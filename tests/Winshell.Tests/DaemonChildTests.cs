using Winshell.Commands;

namespace Winshell.Tests;

[TestClass]
public class DaemonChildTests
{
    [TestMethod]
    [DataRow("-s")]
    [DataRow("--shell")]
    [DataRow("-p")]
    [DataRow("--path")]
    public void A_daemon_child_keeps_a_value_spelled_like_the_flag(string option)
    {
        var parsed = CliParser.Parse(["run", "-d", option, "-d"]);

        Assert.IsTrue(parsed.IsSet(CliFlags.Daemonize));
        Assert.AreEqual("-d", option is "-p" or "--path" ? parsed.Text(CliFlags.Path) : parsed.Text(CliFlags.Shell));
        Assert.AreSequenceEqual(["--no-build", "run", option, "-d"], RunCommand.ChildArguments(parsed));
    }

    [TestMethod]
    public void A_daemon_child_keeps_the_programs_own_flag_after_double_dash()
    {
        var parsed = CliParser.Parse(["run", "-d", "--", "-d"]);

        Assert.AreSequenceEqual<string>(["--no-build", "run", "--", "-d"], RunCommand.ChildArguments(parsed));
        Assert.AreSequenceEqual<string>(["--", "-d"], parsed.Rest);
    }

    [TestMethod]
    public void A_child_that_was_told_not_to_build_is_not_told_again()
    {
        var parsed = CliParser.Parse(["run", "--daemonize", "--no-build", "-s", "-d"]);

        Assert.AreSequenceEqual(["run", "--no-build", "-s", "-d"], RunCommand.ChildArguments(parsed));
    }

    [TestMethod]
    public void A_daemon_start_without_a_command_word_still_names_run()
    {
        var parsed = CliParser.Parse(["-d", "-p", "-d"]);

        Assert.IsNull(parsed.Command);
        Assert.IsTrue(parsed.ShellStartAllowed);
        Assert.AreEqual("-d", parsed.Text(CliFlags.Path));
        Assert.AreSequenceEqual<string>(["--no-build", "run", "-p", "-d"], RunCommand.ChildArguments(parsed));
    }
}
