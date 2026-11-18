namespace Winshell.Tests;

[TestClass]
public class IpcArgumentTests
{
    [TestMethod]
    public void Call_arguments_that_look_like_options_stay_arguments()
    {
        var parsed = CliParser.Parse(["ipc", "call", "notes", "add", "-v", "-p", "50", "--after", "5"]);

        Assert.IsFalse(parsed.Verbose);
        Assert.IsNull(parsed.Text(CliFlags.Path));
        Assert.AreSequenceEqual<string>(["call", "notes", "add", "-v", "-p", "50", "--after", "5"], parsed.Positional);
    }

    [TestMethod]
    public void Options_before_the_function_name_still_count()
    {
        var parsed = CliParser.Parse(["ipc", "-v", "-p", "50", "call", "notes", "add"]);

        Assert.IsTrue(parsed.Verbose);
        Assert.AreEqual("50", parsed.Text(CliFlags.Path));
        Assert.AreSequenceEqual<string>(["call", "notes", "add"], parsed.Positional);
    }

    [TestMethod]
    public void A_flag_between_the_target_and_the_function_is_still_an_option()
    {
        var parsed = CliParser.Parse(["ipc", "call", "notes", "-v", "add"]);

        Assert.IsTrue(parsed.Verbose);
        Assert.AreSequenceEqual<string>(["call", "notes", "add"], parsed.Positional);
    }

    [TestMethod]
    public void Double_dash_ends_options()
    {
        var parsed = CliParser.Parse(["ipc", "show", "-j", "--", "-j", "--force"]);

        Assert.IsTrue(parsed.IsSet(CliFlags.Json));
        Assert.IsFalse(parsed.IsSet(CliFlags.Force));
        Assert.AreSequenceEqual<string>(["show", "-j", "--force"], parsed.Positional);
    }

    [TestMethod]
    [DataRow("kill --help", true)]
    [DataRow("ipc --help", true)]
    [DataRow("--help", true)]
    [DataRow("ipc call target func --help", false)]
    [DataRow("ipc call target --help func", true)]
    public void Help_is_an_option_until_call_arguments_begin(string line, bool help)
    {
        Assert.AreEqual(help, CliParser.Parse(line.Split(' ')).ShowHelp);
    }

    [TestMethod]
    public void Help_after_double_dash_is_an_argument()
    {
        Assert.IsFalse(CliParser.Parse(["--", "--help"]).ShowHelp);
        Assert.IsFalse(CliParser.Parse(["--", "--version"]).ShowVersion);
        Assert.IsTrue(CliParser.Parse(["--version"]).ShowVersion);
        var passed = CliParser.Parse(["--", "kill"]);
        Assert.IsNull(passed.Command);
        Assert.AreSequenceEqual<string>(["--", "kill"], passed.Rest);
        var error = Assert.ThrowsExactly<ArgumentException>(() => CliParser.Parse(["ipc", "--", "call"]));
        Assert.AreEqual("winshell ipc call needs a target and a function.", error.Message);
        var kill = CliParser.Parse(["-c", "work", "kill"]);
        Assert.AreEqual("kill", kill.Command);
        Assert.AreEqual("work", kill.Text(CliFlags.Config));
    }
}
