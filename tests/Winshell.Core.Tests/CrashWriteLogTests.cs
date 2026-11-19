namespace Winshell.Core.Tests;

[TestClass]
public sealed class CrashWriteLogTests : IDisposable
{
    private readonly string _dir =
        TempFolder.NewPath();

    private readonly List<string> _lines = [];

    public CrashWriteLogTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, true);
    }

    private CrashReports Reports(string folder)
    {
        return new CrashReports(folder,
            new Log { Sink = (level, category, message) => _lines.Add($"{level} {category}: {message}") });
    }

    [TestMethod]
    public void A_report_that_cannot_be_written_is_logged_and_still_returns_null()
    {
        var file = Path.Combine(_dir, "not-a-folder");
        File.WriteAllText(file, "x");

        Assert.IsNull(Reports(file).Write("here", new InvalidOperationException("disk full")));

        var line = Assert.ContainsSingle(_lines);
        Assert.StartsWith("Error shell: Could not write a crash report:", line);
        Assert.IsTrue(line.Length > "Error shell: Could not write a crash report:".Length);
    }

    [TestMethod]
    public void A_handler_that_throws_is_logged_and_the_report_is_kept()
    {
        var crashes = Reports(Path.Combine(_dir, "crashes"));
        crashes.Written += _ => throw new InvalidOperationException("notice failed");

        var report = crashes.Write("here", new InvalidOperationException("x"));

        Assert.IsNotNull(report);
        Assert.IsTrue(File.Exists(report.Path));
        var line = Assert.ContainsSingle(_lines);
        Assert.AreEqual("Error shell: Could not write a crash report: notice failed", line);
    }

    [TestMethod]
    public void A_duplicate_report_is_not_logged_as_a_write_failure()
    {
        var crashes = Reports(Path.Combine(_dir, "once"));
        var error = new InvalidOperationException("x");

        Assert.IsNotNull(crashes.WriteOnce("here", error));
        Assert.IsNull(crashes.WriteOnce("here", error));

        Assert.IsEmpty(_lines);
    }
}
