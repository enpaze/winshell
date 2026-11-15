using System.Globalization;

namespace Winshell.Tests;

/// <summary><c>winshell ipc get</c> prints a property the running shell registered.</summary>
public sealed class IpcGetTests : IClassFixture<ProbeBuild>
{
    [Fact]
    public void Get_prints_a_property_of_the_running_shell()
    {
        using var folder = new TempFolder();
        var config = Path.Combine(folder.Path, "config");
        var local = Path.Combine(folder.Path, "local");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(local);
        using var shell = ProbeShell.Start(config, local);
        try
        {
#pragma warning disable CsWinRT1030 // Not marshalled across the WinRT ABI.
            var get = WinshellCli.Run(
                ["ipc", "--pid", shell.Id.ToString(CultureInfo.InvariantCulture), "get", "probe", "name"],
                ("WINSHELL_LOCAL_DIR", local), ("WINSHELL_CONFIG_DIR", config));
#pragma warning restore CsWinRT1030

            get.AssertExitCode(0);
            Assert.Equal("probe", get.Output.Trim());
        }
        finally
        {
            shell.Kill();
            shell.WaitForExit();
        }
    }
}
