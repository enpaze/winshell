using System.Globalization;
using System.Text;

namespace Winshell.Core.Tests;

/// <summary>The next start shows a fatal crash report once, and skips one older than the last clean exit.</summary>
public sealed class CrashFollowUpTests : IDisposable
{
    private readonly string _dir =
        TempFolder.NewPath();

    public CrashFollowUpTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, true);
    }

    [Fact]
    public void A_fatal_report_is_shown_once_and_a_later_one_is_shown_again()
    {
        var folder = Path.Combine(_dir, "crashes");
        Directory.CreateDirectory(folder);
        var now = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        var stray = Path.Combine(folder, "crash-stray.txt");
        var draw = Report(folder, now.AddMinutes(-10), "draw");
        var fatal = Report(folder, now, "fatal");
        File.WriteAllText(stray, """
                                 Winshell crash report
                                 Winshell closed: yes
                                 Exception
                                 System.Exception: stray
                                 """);
        File.WriteAllText(draw, """
                                Winshell crash report
                                Winshell closed: no
                                Exception
                                System.Exception: draw
                                """);
        File.WriteAllText(fatal, """
                                 Winshell crash report
                                 Winshell closed: yes
                                 Exception
                                 System.InvalidOperationException: boom
                                 """);
        File.SetLastWriteTimeUtc(stray, now.AddHours(1));
        File.SetLastWriteTimeUtc(draw, now.AddHours(1));
        File.SetLastWriteTimeUtc(fatal, now.AddHours(-1));
        var state = Path.Combine(_dir, "state", "last-exit.toml");
        CrashReports.MarkClean(state, new DateTimeOffset(now.AddHours(-1), TimeSpan.Zero));

        var pending = CrashReports.Find(folder, state);

        Assert.Equal(fatal, pending?.Path);
        Assert.Equal("System.InvalidOperationException: boom", pending?.ExceptionLine);
        Assert.Contains("clean = ", File.ReadAllText(state), StringComparison.Ordinal);

        CrashReports.MarkShown(state, fatal);
        Assert.Null(CrashReports.Find(folder, state));

        var again = Report(folder, now.AddMinutes(10), "again");
        File.WriteAllText(again, """
                                 Winshell crash report
                                 Winshell closed: yes
                                 Exception
                                 System.Exception: again
                                 """);

        Assert.Equal(again, CrashReports.Find(folder, state)?.Path);
        Assert.Equal("System.Exception: again", CrashReports.Find(folder, state)?.ExceptionLine);
    }

    [Fact]
    public void A_report_older_than_the_clean_exit_is_not_shown()
    {
        var folder = Path.Combine(_dir, "old");
        Directory.CreateDirectory(folder);
        var written = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        var fatal = Report(folder, written, "old");
        File.WriteAllText(fatal, """
                                 Winshell crash report
                                 Winshell closed: yes
                                 Exception
                                 System.Exception: old
                                 """);
        File.SetLastWriteTimeUtc(fatal, written.AddDays(2));
        var state = Path.Combine(_dir, "old-state.toml");
        CrashReports.MarkClean(state, new DateTimeOffset(written.AddMinutes(1), TimeSpan.Zero));

        Assert.Null(CrashReports.Find(folder, state));
        Assert.Null(CrashReports.Find(Path.Combine(_dir, "missing"), state));
    }

    [Fact]
    public void A_follow_up_mark_survives_a_race_with_another_writer()
    {
        var state = Path.Combine(_dir, "race.toml");
        var clean = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        const string shown = @"C:\crashes\crash-one.txt";

        // One pair often finishes in order. Repeat so a lost update between the read and the replace is caught.
        for (var i = 0; i < 40; i++)
        {
            if (File.Exists(state))
                File.Delete(state);
            using var gate = new Barrier(2);
            var failed = new Exception[2];
            var cleanWriter = new Thread(() =>
            {
                try
                {
                    gate.SignalAndWait();
                    CrashReports.MarkClean(state, clean);
                }
                catch (Exception e)
                {
                    failed[0] = e;
                }
            });
            var shownWriter = new Thread(() =>
            {
                try
                {
                    gate.SignalAndWait();
                    CrashReports.MarkShown(state, shown);
                }
                catch (Exception e)
                {
                    failed[1] = e;
                }
            });
            cleanWriter.Start();
            shownWriter.Start();
            cleanWriter.Join();
            shownWriter.Join();
            Assert.Null(failed[0]);
            Assert.Null(failed[1]);

            var text = File.ReadAllText(state);
            Assert.Contains("clean = \"2026-01-02T03:04:05.0000000+00:00\"", text, StringComparison.Ordinal);
            Assert.Contains("shown = \"C:\\\\crashes\\\\crash-one.txt\"", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_state_file_is_written_without_a_byte_order_mark()
    {
        var state = Path.Combine(_dir, "mark.toml");
        CrashReports.MarkClean(state, DateTimeOffset.UnixEpoch);

        var text = File.ReadAllText(state);
        Assert.Equal(Encoding.UTF8.GetByteCount(text), File.ReadAllBytes(state).Length);
    }

    [Fact]
    public void Marking_a_clean_exit_keeps_the_rest_of_the_state_file()
    {
        var state = Path.Combine(_dir, "kept.toml");
        File.WriteAllText(state, """
                                 # stay
                                 shown = "C:\\report.txt"
                                 extra = 1
                                 """);

        CrashReports.MarkClean(state, DateTimeOffset.UnixEpoch);

        var text = File.ReadAllText(state);
        Assert.Contains("# stay", text, StringComparison.Ordinal);
        Assert.Contains("shown = \"C:\\\\report.txt\"", text, StringComparison.Ordinal);
        Assert.Contains("extra = 1", text, StringComparison.Ordinal);
        Assert.Contains("clean = \"1970-01-01T00:00:00.0000000+00:00\"", text, StringComparison.Ordinal);
    }

    private static string Report(string folder, DateTime utc, string slug)
    {
        var name =
            $"crash-{utc.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture)}Z-1-{slug}.txt";
        return Path.Combine(folder, name);
    }
}
