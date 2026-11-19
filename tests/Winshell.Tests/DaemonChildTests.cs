using Winshell.Commands;

namespace Winshell.Tests;

public class DaemonChildTests
{
    [Theory]
    [InlineData("-s")]
    [InlineData("--shell")]
    [InlineData("-p")]
    [InlineData("--path")]
    public void A_daemon_child_keeps_a_value_spelled_like_the_flag(string option)
    {
        var parsed = CliParser.Parse(["run", "-d", option, "-d"]);

        Assert.True(parsed.IsSet(CliFlags.Daemonize));
        Assert.Equal("-d", option is "-p" or "--path" ? parsed.Text(CliFlags.Path) : parsed.Text(CliFlags.Shell));
        Assert.Equal(["--no-build", "run", option, "-d"], RunCommand.ChildArguments(parsed));
    }

    [Fact]
    public void A_daemon_child_keeps_the_programs_own_flag_after_double_dash()
    {
        var parsed = CliParser.Parse(["run", "-d", "--", "-d"]);

        Assert.Equal(["--no-build", "run", "--", "-d"], RunCommand.ChildArguments(parsed));
        Assert.Equal(["--", "-d"], parsed.Rest);
    }

    [Fact]
    public void A_child_that_was_told_not_to_build_is_not_told_again()
    {
        var parsed = CliParser.Parse(["run", "--daemonize", "--no-build", "-s", "-d"]);

        Assert.Equal(["run", "--no-build", "-s", "-d"], RunCommand.ChildArguments(parsed));
    }

    [Fact]
    public void A_daemon_start_without_a_command_word_still_names_run()
    {
        var parsed = CliParser.Parse(["-d", "-p", "-d"]);

        Assert.Null(parsed.Command);
        Assert.True(parsed.ShellStartAllowed);
        Assert.Equal("-d", parsed.Text(CliFlags.Path));
        Assert.Equal(["--no-build", "run", "-p", "-d"], RunCommand.ChildArguments(parsed));
    }
}
