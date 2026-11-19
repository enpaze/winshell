using System.Diagnostics;

namespace Winshell.Core.Tests;

[TestClass]
public sealed class AtomicFileTests
{
    [TestMethod]
    public void A_hand_save_wins_only_when_the_disk_matches_neither_side_of_the_write()
    {
        using var dir = new TempFolder();
        var path = Path.Combine(dir.Path, "settings.toml");
        const string incoming = "name = \"file\"\non = false\n";
        const string read = "name = \"file\"\n";
        const string hand = "name = \"by hand\"\n";

        Assert.AreEqual(AtomicSave.Saved, AtomicFile.Save(path, incoming, false, null));
        Assert.AreEqual(incoming, Text(path));

        File.WriteAllText(path, read);
        Assert.AreEqual(AtomicSave.Saved, AtomicFile.Save(path, incoming, false, Text(path)));
        Assert.AreEqual(incoming, Text(path));

        Assert.AreEqual(AtomicSave.Saved, AtomicFile.Save(path, incoming, false, read));
        Assert.AreEqual(incoming, Text(path));

        File.WriteAllText(path, hand);
        Assert.AreEqual(AtomicSave.HandSaveWon, AtomicFile.Save(path, incoming, false, read));
        Assert.AreEqual(hand, Text(path));
        Assert.IsEmpty(Directory.GetFiles(dir.Path, "settings.toml.tmp-*"));

        Assert.AreEqual(AtomicSave.HandSaveWon, AtomicFile.Save(path, incoming, false, null));
        Assert.AreEqual(hand, Text(path));
    }

    [TestMethod]
    public void A_save_makes_the_folders_the_file_needs()
    {
        using var dir = new TempFolder();
        var path = Path.Combine(dir.Path, "later", "nested", "settings.toml");

        Assert.AreEqual(AtomicSave.Saved, AtomicFile.Save(path, "name = \"mine\"\n", false, null));

        Assert.AreEqual("name = \"mine\"\n", Text(path));
    }

    [TestMethod]
    public async Task A_temp_from_a_dead_process_is_removed_and_a_live_one_stays()
    {
        using var dir = new TempFolder();
        var path = Path.Combine(dir.Path, "preferences.toml");
        var liveGuid = path + ".tmp-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N");
        var other = Path.Combine(dir.Path, "other.toml.tmp-1");
        var junk = path + ".tmp-not-a-pid";
        File.WriteAllText(liveGuid, "live-guid");
        File.WriteAllText(other, "other");
        File.WriteAllText(junk, "junk");

        using var exited = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c exit")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        await WaitUntilExited(exited);
        Assert.IsTrue(exited.HasExited);
        var dead = path + ".tmp-" + exited.Id;
        var deadGuid = path + ".tmp-" + exited.Id + "-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(dead, "dead");
        File.WriteAllText(deadGuid, "dead-guid");

        Assert.AreEqual(AtomicSave.Saved, AtomicFile.Save(path, "kept\n", false, null));

        Assert.IsFalse(File.Exists(dead));
        Assert.IsFalse(File.Exists(deadGuid));
        Assert.IsFalse(File.Exists(junk));
        Assert.IsTrue(File.Exists(liveGuid));
        Assert.IsTrue(File.Exists(other));
    }

    [TestMethod]
    public void Each_save_through_a_hard_link_updates_both_names()
    {
        using var dir = new TempFolder();
        var (real, link) = Pair(dir);
        File.WriteAllText(real, "old\n");
        HardLink(link, real);

        foreach (var text in new[] { "one\n", "two\n" })
        {
            Assert.IsTrue(Save(link, text));

            Assert.Contains(text, File.ReadAllText(real), StringComparison.Ordinal);
            Assert.Contains(text, File.ReadAllText(link), StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void Each_save_through_a_symbolic_link_updates_the_target_and_keeps_the_link()
    {
        using var dir = new TempFolder();
        var (real, link) = Pair(dir);
        File.WriteAllText(real, "old\n");
        SymbolicLink(link, real);

        foreach (var text in new[] { "one\n", "two\n" })
        {
            Assert.IsTrue(Save(link, text));

            Assert.Contains(text, File.ReadAllText(real), StringComparison.Ordinal);
            Assert.IsNotNull(new FileInfo(link).LinkTarget);
            Assert.Contains(text, File.ReadAllText(link), StringComparison.Ordinal);
        }
    }

    private static async Task WaitUntilExited(Process process)
    {
        var token = CurrentTest.Cancellation;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            process.Kill(true);
            Assert.Fail("cmd.exe did not exit.");
        }
    }

    private static bool Save(string path, string text)
    {
        var expected = File.Exists(path) ? File.ReadAllText(path) : null;
        return AtomicFile.Save(path, text, false, expected) == AtomicSave.Saved;
    }

    private static string Text(string path)
    {
        return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static (string Real, string Link) Pair(TempFolder dir)
    {
        return (Path.Combine(dir.Path, "real.toml"), Path.Combine(dir.Path, "link.toml"));
    }

    private static void SymbolicLink(string link, string real)
    {
        ChildProcess.Run("cmd.exe", "/c", "mklink", link, real).AssertExitCode(0);
    }

    private static void HardLink(string link, string real)
    {
        ChildProcess.Run("cmd.exe", "/c", "mklink", "/H", link, real).AssertExitCode(0);
    }
}
