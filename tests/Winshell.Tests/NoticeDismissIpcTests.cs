using System.Diagnostics;
using System.Globalization;

namespace Winshell.Tests;

[TestClass]
public sealed partial class NoticeDismissIpcTests : ProbeBuild, IDisposable
{
    private readonly string _dir = TempFolder.NewPath();
    private Process? _started;

    [TestMethod]
    public void Notice_dismiss_reaches_a_running_shell_and_takes_no_arguments()
    {
        var config = Path.Combine(_dir, "config");
        Directory.CreateDirectory(config);
        var process = _started = ProbeShell.Start(config, Path.Combine(_dir, "local"));

        var pid = process.Id.ToString(CultureInfo.InvariantCulture);
        var dismissed = Run("ipc", "--pid", pid, "call", "notice", "dismiss");
        dismissed.AssertNoError();
        Assert.AreEqual("", dismissed.Output.Trim());
        dismissed.AssertExitCode(0);
        Assert.IsFalse(process.HasExited);

        var extra = Run("ipc", "--pid", pid, "call", "notice", "dismiss", "now");
        extra.AssertExitCode(1);
        Assert.Contains("Function notice.dismiss takes 0 arguments, 1 given.", extra.Error, StringComparison.Ordinal);
        Assert.IsFalse(process.HasExited);

        var other = Run("ipc", "--pid", pid, "call", "notice", "open");
        other.AssertExitCode(1);
        Assert.Contains("Target not found: notice.", other.Error, StringComparison.Ordinal);
        Assert.IsFalse(process.HasExited);
    }

    private ChildProcess Run(params string[] args)
    {
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(args, IsolatedFolders.Under(_dir));
#pragma warning restore CsWinRT1030
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
