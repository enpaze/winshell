namespace Winshell.Core.Tests;

public sealed class DirectoryWatchTests : IDisposable
{
    private readonly string _directory =
        TempFolder.NewPath();

    public DirectoryWatchTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void A_file_created_before_the_first_wait_is_reported()
    {
        File.WriteAllText(Path.Combine(_directory, "made.txt"), "");
        Directory.CreateDirectory(Path.Combine(_directory, "made-dir"));

        using var files = DirectoryWatch.Start(_directory, DirectoryWatch.FileChanges, _ => { }, () => { });
        using var names = DirectoryWatch.Start(_directory, DirectoryWatch.NameChanges, _ => { }, () => { });

        Assert.NotNull(files);
        Assert.Contains("made.txt", files.Existing);
        Assert.DoesNotContain("made-dir", files.Existing);
        Assert.NotNull(names);
        Assert.Contains("made.txt", names.Existing);
        Assert.Contains("made-dir", names.Existing);
    }

    [Fact]
    public void A_file_made_right_after_start_is_reported()
    {
        using var seen = new ManualResetEventSlim();
        using var watch = DirectoryWatch.Start(_directory, DirectoryWatch.FileChanges, name =>
        {
            if (name == "made.txt")
                // Disposing the watch stops this callback before the event is disposed.
                seen.Set();
        }, () => { });
        Assert.NotNull(watch);

        File.WriteAllText(Path.Combine(_directory, "made.txt"), "");

        Assert.True(seen.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken),
            "The new file was not reported.");
    }

    [Fact]
    public void A_watch_outlives_the_thread_that_started_it()
    {
        using var seen = new ManualResetEventSlim();
        DirectoryWatch? watch = null;
        var starter = new Thread(() => watch = DirectoryWatch.Start(_directory, DirectoryWatch.FileChanges, name =>
        {
            if (name == "made.txt")
                // Disposing the watch stops this callback before the event is disposed.
                seen.Set();
        }, () => { }));
        starter.Start();
        starter.Join();
        using (watch)
        {
            Assert.NotNull(watch);

            File.WriteAllText(Path.Combine(_directory, "made.txt"), "");

            Assert.True(seen.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken),
                "The new file was not reported.");
        }
    }

    [Fact]
    public void Deleting_the_folder_ends_the_watch()
    {
        using var lost = new ManualResetEventSlim();
        using var watch = DirectoryWatch.Start(_directory, DirectoryWatch.FileChanges, _ => { }, lost.Set);
        Assert.NotNull(watch);

        Directory.Delete(_directory, true);

        Assert.True(lost.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken),
            "The watch did not report that it ended.");
    }

    [Fact]
    public void A_missing_folder_is_not_watched()
    {
        Assert.Null(DirectoryWatch.Start(Path.Combine(_directory, "missing"), DirectoryWatch.FileChanges, _ => { },
            () => { }));
    }

    [Fact]
    public void Disposing_ends_the_watch_without_reporting_it()
    {
        var lost = false;
        var watch = DirectoryWatch.Start(_directory, DirectoryWatch.FileChanges, _ => { }, () => lost = true);
        Assert.NotNull(watch);

        watch.Dispose();

        Assert.False(lost);
    }

    [Fact]
    public void A_file_watch_disposed_twice_is_harmless()
    {
        var watch = new FileWatch(Path.Combine(_directory, "watched.txt"), new ManualEventLoop(), () => { });
        watch.Dispose();

        var second = Record.Exception(watch.Dispose);

        Assert.Null(second);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }
}
