using System.Diagnostics;
using System.Globalization;

namespace Winshell.Tests;

/// <summary><c>winshell kill</c> waits until the instance's process has exited.</summary>
public sealed partial class KillWaitTests : IDisposable, IClassFixture<ProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();
    private readonly List<Process> _started = [];

    [Fact]
    public void Force_ends_the_shell_and_leaves_the_programs_it_started()
    {
        var config = Path.Combine(_dir, "config");
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, ProbeShell.HangMarker), "");
        var process = ProbeShell.Start(config, Path.Combine(_dir, "local"), "hung");
        _started.Add(process);
        var pidFile = Path.Combine(config, "child.pid");
        Wait.Until(() => File.Exists(pidFile), "the shell's program to start");
        using var child = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile), CultureInfo.InvariantCulture));
        try
        {
            var result = Run("kill", "--force", "--pid", process.Id.ToString(CultureInfo.InvariantCulture));

            result.AssertExitCode(0);
            Assert.True(process.HasExited);
            Assert.False(child.HasExited);
        }
        finally
        {
            child.Kill();
            child.WaitForExit(5000);
        }
    }

    private ChildProcess Run(params string[] args)
    {
#pragma warning disable CsWinRT1030 // Not marshalled across the WinRT ABI.
        return WinshellCli.Run(args, ("WINSHELL_LOCAL_DIR", Path.Combine(_dir, "local")),
            ("WINSHELL_CONFIG_DIR", Path.Combine(_dir, "config")));
#pragma warning restore CsWinRT1030
    }

    public void Dispose()
    {
        foreach (var process in _started)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(true);
                }
                // The process can exit between the check and Kill.
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }

                process.WaitForExit(5000);
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
