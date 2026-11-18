namespace Winshell.Tests;

public class UnknownOptionTests
{
    [Fact]
    public void A_bad_command_line_exits_2_with_only_its_message()
    {
        var missing = TempFolder.NewPath();

        var result = WinshellCli.RunWithEmptyConfig(["run", "-p", missing, "--pth"]);

        result.AssertExitCode(2);
        Assert.Equal("", result.Output);
        Assert.Equal("Unknown option \"--pth\"." + Environment.NewLine, result.Error);
    }
}
