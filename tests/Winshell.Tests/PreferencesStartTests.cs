using System.Diagnostics;

namespace Winshell.Tests;

public sealed class PreferencesStartTests : IDisposable, IClassFixture<ProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();
    private Process? _started;

    [Fact]
    public void The_file_written_when_start_returns_includes_a_section_posted_during_start()
    {
        var config = Path.Combine(_dir, "config");
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, ProbeShell.PreferencesMarker), "");
        _started = ProbeShell.Start(config, Path.Combine(_dir, "local"));

        var report = Path.Combine(config, "preferences-report.txt");
        string? said = null;
        Wait.Until(() => (said = Read(report)) is not null,
            "the probe to report the preferences file written when start returns");
        Assert.Equal("both", said);
    }

    private static string? Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_started is { } process)
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit();
            }

            process.Dispose();
        }

        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }
}
