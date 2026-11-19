using System.Text.RegularExpressions;

namespace Winshell.Tests;

[TestClass]
public class VersionTests
{
    [TestMethod]
    [DataRow("--version")]
    [DataRow("list --version")]
    public void The_cli_prints_the_release_please_version(string line)
    {
        var manifest = Repo.ReleaseVersion();
#pragma warning disable CsWinRT1030
        var result = WinshellCli.Run(line.Split(' '));
#pragma warning restore CsWinRT1030
        result.AssertExitCode(0);
        result.AssertNoError();
        Assert.MatchesRegex($"^winshell {Regex.Escape(manifest)}(\\+\\S+)?\\r?\\n$", result.Output);
    }

    [TestMethod]
    [DataRow("-V")]
    [DataRow("list -V")]
    public void The_short_version_option_is_unknown(string line)
    {
#pragma warning disable CsWinRT1030
        var result = WinshellCli.Run(line.Split(' '));
#pragma warning restore CsWinRT1030
        Assert.AreEqual((2, "", "Unknown option \"-V\"." + Environment.NewLine), (result.Code, result.Output, result.Error));
    }
}
