using System.Buffers.Binary;
using System.Globalization;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class TempImageStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Cleanup_leaves_files_reached_through_a_directory_link_alone(bool linkedRoot)
    {
        var directory = TempFolder.NewPath();
        var cache = Path.Combine(directory, "cache");
        var elsewhere = Path.Combine(directory, "elsewhere");
        Directory.CreateDirectory(cache);
        Directory.CreateDirectory(elsewhere);
        var outside = Path.Combine(elsewhere, new string('a', 32) + ".png");
        File.WriteAllText(outside, "not the cache's to delete");
        File.SetLastWriteTimeUtc(outside, Now.UtcDateTime.AddDays(-3));
        var link = linkedRoot ? Path.Combine(directory, "linked-cache") : Path.Combine(cache, "1234");
        try
        {
            ChildProcess.Run("cmd.exe", "/c", "mklink", "/J", link, elsewhere).AssertExitCode(0);
            Assert.IsNotNull(new DirectoryInfo(link).LinkTarget);

            Store(linkedRoot ? link : cache).CleanOld();

            Assert.IsTrue(File.Exists(outside), "Cleanup followed a junction and removed a file outside the cache.");
        }
        finally
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Cleanup_removes_pictures_older_than_a_day_by_the_store_clock()
    {
        using var directory = new TempFolder();
        var processFolder = Path.Combine(directory.Path, "1234");
        Directory.CreateDirectory(processFolder);
        var old = Path.Combine(processFolder, new string('a', 32) + ".png");
        var recent = Path.Combine(processFolder, new string('b', 32) + ".png");
        File.WriteAllText(old, "old");
        File.SetLastWriteTimeUtc(old, Now.UtcDateTime.AddDays(-1).AddMinutes(-1));
        File.WriteAllText(recent, "recent");
        File.SetLastWriteTimeUtc(recent, Now.UtcDateTime.AddDays(-1).AddMinutes(1));

        Store(directory.Path).CleanOld();

        Assert.IsFalse(File.Exists(old));
        Assert.IsTrue(File.Exists(recent));
    }

    [TestMethod]
    public void A_file_cleanup_cannot_delete_is_reported_and_the_rest_are_still_deleted()
    {
        using var directory = new TempFolder();
        var processFolder = Path.Combine(directory.Path, "1234");
        Directory.CreateDirectory(processFolder);
        var locked = Path.Combine(processFolder, new string('a', 32) + ".png");
        var free = Path.Combine(processFolder, new string('b', 32) + ".png");
        foreach (var file in new[] { locked, free })
        {
            File.WriteAllText(file, "old");
            File.SetLastWriteTimeUtc(file, Now.UtcDateTime.AddDays(-3));
        }

        string? failure;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            failure = Store(directory.Path).CleanOld();
        }

        Assert.IsFalse(File.Exists(free));
        Assert.StartsWith("Cannot clean old pictures", failure, StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_same_picture_is_one_file_in_this_process_folder_until_its_last_release()
    {
        using var directory = new TempFolder();
        var store = Store(directory.Path);
        var png = Png(2, 2);
        var url = Claim(store, png);
        Assert.IsNotNull(url);
        var path = new Uri(url).LocalPath;
        Assert.AreEqual(Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            Path.GetFileName(Path.GetDirectoryName(path)));
        Assert.EndsWith(".png", path, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(url, Claim(store, png));

        store.Release(url);
        Assert.IsTrue(File.Exists(path));
        store.Release(url);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void No_picture_is_no_url()
    {
        using var directory = new TempFolder();
        Assert.AreEqual("", Claim(Store(directory.Path), []));
    }

    [TestMethod]
    public void A_picture_at_the_pixel_cap_is_stored_and_one_pixel_over_is_not()
    {
        using var directory = new TempFolder();
        var store = Store(directory.Path);
        var kept = Claim(store, Png(4096, 4096));
        Assert.IsNotNull(kept);
        Assert.EndsWith(".png", new Uri(kept).LocalPath, StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(File.Exists(new Uri(kept).LocalPath));

        Assert.IsNull(Claim(store, Png(4097, 4096)));
        store.Release(kept);
        Assert.IsFalse(File.Exists(new Uri(kept).LocalPath));
    }

    [TestMethod]
    public void A_file_still_open_at_the_last_release_is_reused()
    {
        using var directory = new TempFolder();
        var store = Store(directory.Path);
        var png = Png(2, 2);
        var url = Claim(store, png);
        Assert.IsNotNull(url);
        var path = new Uri(url).LocalPath;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            store.Release(url);
            Assert.AreEqual(url, Claim(store, png));
            Assert.AreSequenceEqual(png, File.ReadAllBytes(path));
        }

        store.Release(url);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void A_picture_with_no_pixels_is_not_stored()
    {
        using var directory = new TempFolder();
        Assert.IsNull(Claim(Store(directory.Path), Png(0, 2)));
        Assert.IsEmpty(Directory.EnumerateFiles(directory.Path, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public void A_picture_over_the_byte_cap_is_not_stored()
    {
        var art = new byte[TempImageStore.MaxBytes + 1];
        Png(2, 2).CopyTo(art, 0);
        using var directory = new TempFolder();

        Assert.IsNull(Claim(Store(directory.Path), art));
        Assert.IsEmpty(Directory.EnumerateFiles(directory.Path, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public void Svg_is_not_stored()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100000\" height=\"100000\"/>"u8.ToArray();
        using var directory = new TempFolder();

        Assert.IsNull(Claim(Store(directory.Path), svg));
        Assert.IsEmpty(Directory.EnumerateFiles(directory.Path, "*", SearchOption.AllDirectories));
    }

    private static TempImageStore Store(string root)
    {
        return new TempImageStore(root, new FakeTimeProvider(Now));
    }

    private static string? Claim(TempImageStore store, byte[] data)
    {
        return store.Claim(new MemoryStream(data), CurrentTest.Cancellation);
    }

    private static byte[] Png(int width, int height)
    {
        var data = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 }.CopyTo(data, 0);
        "IHDR"u8.CopyTo(data.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), (uint)height);
        return data;
    }
}
