using System.Globalization;

namespace Winshell.Core.Tests;

[TestClass]
public sealed class CrashTests : IDisposable
{
    private readonly string _folder =
        TempFolder.NewPath();

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    private static Exception Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException e)
        {
            return e;
        }
    }

    [TestMethod]
    public void A_report_says_where_what_and_the_end_of_the_log()
    {
        Directory.CreateDirectory(_folder);
        var log = Path.Combine(_folder, "log.txt");
        File.WriteAllLines(log, Enumerable.Range(1, 100).Select(i => $"12:00:00.000  INFO window: line {i}"));
        var crashes = new CrashReports(Path.Combine(_folder, "crashes"), shellName: "my-shell",
            program: @"C:\config\MyShell.exe", logPath: log);
        CrashReport? raised = null;
        crashes.Written += r => raised = r;

        var report = crashes.Write("PanelWindow in Taskbar.xaml", Thrown("the paint failed"));

        Assert.IsNotNull(report);
        Assert.AreSame(report, raised);
        Assert.IsFalse(report.Fatal);
        Assert.StartsWith(Path.Combine(_folder, "crashes"), report.Path, StringComparison.Ordinal);
        Assert.EndsWith("-panelwindow-in-taskbar-xaml.txt", report.Path, StringComparison.Ordinal);
        var text = File.ReadAllText(report.Path);
        Assert.StartsWith("Winshell crash report", text, StringComparison.Ordinal);
        Assert.Contains("This file stays on this PC.", text, StringComparison.Ordinal);
        Assert.Contains("Winshell: " + ProductVersion.Current.Text, text, StringComparison.Ordinal);
        Assert.Contains("Windows: ", text, StringComparison.Ordinal);
        Assert.Contains("Shell name: my-shell", text, StringComparison.Ordinal);
        Assert.Contains(@"Program: C:\config\MyShell.exe", text, StringComparison.Ordinal);
        Assert.Contains("Where: PanelWindow in Taskbar.xaml", text, StringComparison.Ordinal);
        Assert.Contains("Winshell closed: no", text, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException: the paint failed", text, StringComparison.Ordinal);
        Assert.Contains(nameof(Thrown), text, StringComparison.Ordinal);
        Assert.Contains("last 40 lines", text, StringComparison.Ordinal);
        Assert.Contains("line 100", text, StringComparison.Ordinal);
        Assert.Contains("line 61", text, StringComparison.Ordinal);
        Assert.DoesNotContain("line 60" + Environment.NewLine, text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_same_failure_from_the_same_place_is_reported_once()
    {
        var crashes = new CrashReports(_folder);

        Exception Same()
        {
            return Thrown("again");
        }

        Assert.IsNotNull(crashes.WriteOnce("event loop", Same()));
        Assert.IsNull(crashes.WriteOnce("event loop", Same()));
        Assert.IsNotNull(crashes.WriteOnce("background task", Same()));
        Assert.AreEqual(2, Directory.GetFiles(_folder).Length);
    }

    [TestMethod]
    public void A_report_is_read_back_by_the_time_in_its_name()
    {
        Directory.CreateDirectory(_folder);
        var older = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        var newer = older.AddMinutes(5);
        var olderPath = Path.Combine(_folder, Name(older, "old"));
        var newerPath = Path.Combine(_folder, Name(newer, "new"));
        File.WriteAllText(olderPath, """
                                     Winshell crash report
                                     Winshell closed: yes
                                     Exception
                                     System.Exception: old
                                     """);
        File.WriteAllText(newerPath, """
                                     Winshell crash report
                                     Winshell closed: no
                                     Exception
                                     System.Exception: new
                                     """);
        File.WriteAllText(Path.Combine(_folder, "crash-stray.txt"), """
                                                                    Winshell crash report
                                                                    Winshell closed: yes
                                                                    """);
        File.SetLastWriteTimeUtc(olderPath, newer.AddHours(1));
        File.SetLastWriteTimeUtc(newerPath, older);

        var list = CrashReports.List(_folder);

        Assert.IsNull(list.FolderError);
        Assert.AreEqual(newerPath, list.Reports[0].Path);
        Assert.IsFalse(list.Reports[0].Closed);
        Assert.AreEqual("System.Exception: new", list.Reports[0].ExceptionLine);
        Assert.AreEqual(olderPath, list.Reports[1].Path);
        Assert.IsTrue(list.Reports[1].Closed);
        Assert.AreEqual("System.Exception: old", list.Reports[1].ExceptionLine);
        Assert.AreEqual(new DateTimeOffset(older, TimeSpan.Zero), list.Reports[1].Written);
    }

    private static string Name(DateTime utc, string slug)
    {
        return $"crash-{utc.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture)}Z-1-{slug}.txt";
    }
}
