namespace Winshell.Tests;

public class CliHelpTests
{
    [Theory]
    [InlineData("list", "--help", "list")]
    [InlineData("stop", "--help", "stop")]
    [InlineData("stop", "-h", "stop")]
    [InlineData("stop", "/?", "stop")]
    [InlineData("log", "--help", "log")]
    [InlineData("ipc", "--help", "ipc")]
    [InlineData("doctor", "--help", "doctor")]
    [InlineData("help", "--help", "Usage")]
    [InlineData("--help", null, "Usage")]
    [InlineData("/?", null, "Usage")]
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
