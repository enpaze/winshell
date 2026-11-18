using System.Diagnostics;
using System.Globalization;

namespace Winshell.Tests;

[TestClass]
public sealed partial class IpcGetTests : ProbeBuild, IDisposable
{
    private readonly string _dir = TempFolder.NewPath();
    private Process? _started;

    [TestMethod]
    public void Get_prints_a_property_of_the_running_shell()
    {
        var config = Path.Combine(_dir, "config");
        Directory.CreateDirectory(config);
        var process = _started = ProbeShell.Start(config, Path.Combine(_dir, "local"));

        var get = Run("ipc", "--pid", process.Id.ToString(CultureInfo.InvariantCulture), "get", "probe", "name");

        get.AssertExitCode(0);
        Assert.AreEqual("probe", get.Output.Trim());
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
