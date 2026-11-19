namespace Winshell.Core.Tests;

[TestClass]
public sealed class DirectoryWatchTests : IDisposable
{
    private readonly string _directory =
        TempFolder.NewPath();

    public DirectoryWatchTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [TestMethod]
    public void A_file_and_a_folder_already_there_are_listed()
    {
        File.WriteAllText(Path.Combine(_directory, "made.txt"), "");
        Directory.CreateDirectory(Path.Combine(_directory, "made-dir"));

        using var files = DirectoryWatch.Start(_directory, DirectoryWatch.FileChanges, _ => { }, () => { });
        using var names = DirectoryWatch.Start(_directory, DirectoryWatch.NameChanges, _ => { }, () => { });

        Assert.IsNotNull(files);
        Assert.Contains("made.txt", files.ExistingNames);
        Assert.DoesNotContain("made-dir", files.ExistingNames);
        Assert.IsNotNull(names);
        Assert.Contains("made.txt", names.ExistingNames);
        Assert.Contains("made-dir", names.ExistingNames);
    }

    [TestMethod]
    public void A_file_made_right_after_start_is_reported()
    {
        using var seen = new ManualResetEventSlim();
        using var watch = DirectoryWatch.Start(_directory, DirectoryWatch.FileChanges, name =>
        {
            if (name == "made.txt")
                seen.Set();
        }, () => { });
        Assert.IsNotNull(watch);

        File.WriteAllText(Path.Combine(_directory, "made.txt"), "");

        Assert.IsTrue(seen.Wait(TimeSpan.FromSeconds(10), CurrentTest.Cancellation),
            "The new file was not reported.");
    }

    [TestMethod]
    public void A_watch_outlives_the_thread_that_started_it()
    {
        using var seen = new ManualResetEventSlim();
        DirectoryWatch? watch = null;
        var starter = new Thread(() => watch = DirectoryWatch.Start(_directory, DirectoryWatch.FileChanges, name =>
        {
            if (name == "made.txt")
                seen.Set();
        }, () => { }));
        starter.Start();
        starter.Join();
        using (watch)
        {
            Assert.IsNotNull(watch);

            File.WriteAllText(Path.Combine(_directory, "made.txt"), "");

            Assert.IsTrue(seen.Wait(TimeSpan.FromSeconds(10), CurrentTest.Cancellation),
                "The new file was not reported.");
        }
    }

    [TestMethod]
    public void Deleting_the_folder_ends_the_watch()
    {
        using var lost = new ManualResetEventSlim();
        using var watch = DirectoryWatch.Start(_directory, DirectoryWatch.FileChanges, _ => { }, lost.Set);
        Assert.IsNotNull(watch);

        Directory.Delete(_directory, true);

        Assert.IsTrue(lost.Wait(TimeSpan.FromSeconds(10), CurrentTest.Cancellation),
            "The watch did not report that it ended.");
    }

    [TestMethod]
    public void A_missing_folder_is_not_watched()
    {
        Assert.IsNull(DirectoryWatch.Start(Path.Combine(_directory, "missing"), DirectoryWatch.FileChanges, _ => { },
            () => { }));
    }

    [TestMethod]
    public void Disposing_ends_the_watch_without_reporting_it()
    {
        var lost = false;
        var watch = DirectoryWatch.Start(_directory, DirectoryWatch.FileChanges, _ => { }, () => lost = true);
        Assert.IsNotNull(watch);

        watch.Dispose();

        Assert.IsFalse(lost);
    }

    [TestMethod]
    public void A_file_watch_disposed_twice_is_harmless()
    {
        var watch = new FileWatch(Path.Combine(_directory, "watched.txt"), new ManualEventLoop(), () => { });
        watch.Dispose();

        watch.Dispose();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }
}
