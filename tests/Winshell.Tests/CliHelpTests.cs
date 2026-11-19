namespace Winshell.Tests;

[TestClass]
public class CliHelpTests
{
    [TestMethod]
    [DataRow("list", "--help", "list")]
    [DataRow("stop", "--help", "stop")]
    [DataRow("stop", "-h", "stop")]
    [DataRow("stop", "/?", "stop")]
    [DataRow("log", "--help", "log")]
    [DataRow("ipc", "--help", "ipc")]
    [DataRow("doctor", "--help", "doctor")]
    [DataRow("help", "--help", "Usage")]
    [DataRow("--help", null, "Usage")]
    [DataRow("/?", null, "Usage")]
    public void Help_exits_0_and_does_not_run_the_command(string command, string? help, string expected)
    {
        var args = help is null ? new[] { command } : new[] { command, help };
        var result = WinshellCli.RunWithEmptyConfig(args);

        result.AssertExitCode(0);
        Assert.Contains(expected, result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("No running instances", result.Output, StringComparison.Ordinal);
        if (command == "--help")
            Assert.Contains("2 the command line was wrong", result.Output, StringComparison.Ordinal);
        result.AssertNoError();
    }
}
