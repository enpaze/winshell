using System.Diagnostics;

namespace Winshell.Tests;

/// <summary>
/// The preferences file is written when <see cref="ShellConfiguration.Start"/> returns, including a section that was
/// posted during Start. The probe opens no window.
/// </summary>
public sealed class PreferencesStartTests : IDisposable, IClassFixture<ProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();
    private Process? _started;

    [Fact]
    public void The_file_written_when_Start_returns_includes_a_section_posted_during_Start()
    {
        var config = Path.Combine(_dir, "config");
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, ProbeShell.PreferencesMarker), "");
        var shellId = Guid.NewGuid().ToString("N");
        _started = ProbeShell.Start(config, Path.Combine(_dir, "local"), shellId);

        var report = Path.Combine(config, "preferences-report.txt");
        Wait.Until(() => File.Exists(report) && File.ReadAllText(report).Trim() is "both" or "missing" or "partial",
            "the probe to report the preferences file written when Start returns");
        Assert.Equal("both", File.ReadAllText(report).Trim());
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
