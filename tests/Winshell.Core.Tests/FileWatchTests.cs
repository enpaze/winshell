namespace Winshell.Core.Tests;

/// <summary>A <see cref="FileWatch"/> follows a file whose folder is made, or deleted and made again, after it starts, and calls back on its loop.</summary>
public sealed class FileWatchTests
{
    /// <summary>The file's text, or null when it is missing. A file held by its writer keeps what was read before.</summary>
    private static string? Read(string path, string? before)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return before;
        }
    }

    [Theory]
    [InlineData("later")]
    [InlineData("later/nested/config")]
    public void A_file_made_in_a_new_folder_is_reported(string folder)
    {
        using var dir = new TempFolder();
        var loop = new ManualEventLoop();
        var path = Path.Combine(dir.Path, folder, "settings.toml");
        string? read = null;
        var threads = new HashSet<int>();
        using var watch = new FileWatch(path, loop, () =>
        {
            threads.Add(Environment.CurrentManagedThreadId);
            read = Read(path, read);
        });

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "by hand");

        Wait.Until(() => read == "by hand", "the file in the new folder", loop.Process);
        // Only the loop calls back, and only this thread turns it.
        Assert.Equal([Environment.CurrentManagedThreadId], threads);
    }

    [Fact]
    public void A_file_in_a_folder_deleted_and_made_again_is_reported()
    {
        using var dir = new TempFolder();
        var loop = new ManualEventLoop();
        var folder = Path.Combine(dir.Path, "config");
        var path = Path.Combine(folder, "settings.toml");
        Directory.CreateDirectory(folder);
        File.WriteAllText(path, "first");
        string? read = "first";
        using var watch = new FileWatch(path, loop, () => read = Read(path, read));

        Directory.Delete(folder, true);
        Wait.Until(() => read is null, "the deleted folder", loop.Process);

        Directory.CreateDirectory(folder);
        File.WriteAllText(path, "second");
        Wait.Until(() => read == "second", "the file in the folder made again", loop.Process);
    }

    [Fact]
    public void A_file_named_like_a_missing_folder_does_not_rewatch()
    {
        using var dir = new TempFolder();
        var loop = new EventQueue();
        File.WriteAllText(Path.Combine(dir.Path, "config"), "not a folder");
        var reads = 0;
        using var watch = new FileWatch(Path.Combine(dir.Path, "config", "settings.toml"), loop, () => reads++);

        Assert.False(loop.RunDue());
        Assert.Equal(0, reads);
    }
}
