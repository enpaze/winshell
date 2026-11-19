using System.Globalization;

namespace Winshell.Tests;

public sealed partial class DetachOutputTests : IDisposable, IClassFixture<ProbeBuild>
{
    private readonly string _dir = TempFolder.NewPath();

    private (string, string)[] Folders => IsolatedFolders.Under(_dir);

    public DetachOutputTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "config"));
        Directory.CreateDirectory(Path.Combine(_dir, "local"));
    }

    [Fact]
    public void The_output_of_run_d_ends_when_it_returns()
    {
        var project = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ProbeShell.Program)))!;

        var result = WinshellCli.Run(["run", "-d", "--no-build", "-p", project], Folders);

        result.AssertExitCode(0);
        result.AssertNoError();
        Wait.Until(() => Running().Count > 0, "the shell -d left behind");
    }

    private List<int> Running()
    {
        return new Instances(Path.Combine(_dir, "local", "run")).Running().Select(info => info.Pid).ToList();
    }

    public void Dispose()
    {
        foreach (var pid in Running())
            WinshellCli.Run(["stop", "--force", "--pid", pid.ToString(CultureInfo.InvariantCulture)], Folders);
        Wait.Until(Removed, "the test folder to be free");
    }

    private bool Removed()
    {
        try
        {
            Directory.Delete(_dir, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
