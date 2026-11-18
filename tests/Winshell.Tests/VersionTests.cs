using System.Text.RegularExpressions;

namespace Winshell.Tests;

[TestClass]
public class VersionTests
{
    [TestMethod]
    [DataRow("--version")]
    [DataRow("list -V")]
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
}
