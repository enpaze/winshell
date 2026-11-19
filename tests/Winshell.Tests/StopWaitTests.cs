using System.Diagnostics;
using System.Globalization;

namespace Winshell.Tests;

[TestClass]
public sealed partial class StopWaitTests : ProbeBuild, IDisposable
{
    private readonly string _dir = TempFolder.NewPath();
    private readonly List<Process> _started = [];

    [TestMethod]
    public void Force_ends_the_shell_and_leaves_the_programs_it_started()
    {
        var config = Path.Combine(_dir, "config");
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, ProbeShell.HangMarker), "");
        var process = ProbeShell.Start(config, Path.Combine(_dir, "local"));
        _started.Add(process);
        var pidFile = Path.Combine(config, "child.pid");
        Wait.Until(() => File.Exists(pidFile), "the shell's program to start");
        using var child = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile), CultureInfo.InvariantCulture));
        try
        {
            var result = Run("stop", "--force", "--pid", process.Id.ToString(CultureInfo.InvariantCulture));

            result.AssertExitCode(0);
            Assert.IsTrue(process.HasExited);
            Assert.IsFalse(child.HasExited);
        }
        finally
        {
            child.Kill();
            child.WaitForExit(5000);
        }
    }

    private ChildProcess Run(params string[] args)
    {
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(args, IsolatedFolders.Under(_dir));
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
