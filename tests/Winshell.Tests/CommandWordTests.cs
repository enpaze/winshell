namespace Winshell.Tests;

[TestClass]
public class CommandWordTests
{
    [TestMethod]
    [DataRow("foo bar", "foo")]
    [DataRow("-v foo bar", "foo")]
    [DataRow("msg notes add", "msg")]
    [DataRow("foo --help", "foo")]
    [DataRow("widget install enpaze/widgets", "widget")]
    public void An_unknown_command_is_named_even_with_words_after_it(string line, string word)
    {
        var e = Assert.ThrowsExactly<ArgumentException>(() => CliParser.Parse(line.Split(' ')));

        Assert.AreEqual($"Unknown command \"{word}\". Run winshell --help.", e.Message);
    }

    [TestMethod]
    [DataRow("ipc bogus", "Unknown ipc command \"bogus\". Run winshell ipc --help.")]
    [DataRow("ipc bogus target func", "Unknown ipc command \"bogus\". Run winshell ipc --help.")]
    [DataRow("ipc prop get notes count", "Unknown ipc command \"prop\". Run winshell ipc --help.")]
    [DataRow("inspect bogus", "Unknown inspect command \"bogus\". Run winshell inspect --help.")]
    public void An_unknown_subcommand_is_named(string line, string message)
    {
        var e = Assert.ThrowsExactly<ArgumentException>(() => CliParser.Parse(line.Split(' ')));

        Assert.AreEqual(message, e.Message);
    }

    [TestMethod]
    [DataRow("ipc", "winshell ipc needs a command. Run winshell ipc --help.")]
    [DataRow("inspect", "winshell inspect needs a command. Run winshell inspect --help.")]
    [DataRow("ipc call", "winshell ipc call needs a target and a function.")]
    [DataRow("ipc call notes", "winshell ipc call needs a target and a function.")]
    [DataRow("ipc get", "winshell ipc get needs a target and a property.")]
    [DataRow("ipc get notes", "winshell ipc get needs a target and a property.")]
    [DataRow("ipc wait notes", "winshell ipc wait needs a target and a signal.")]
    [DataRow("ipc listen", "winshell ipc listen needs a target and a signal.")]
    [DataRow("inspect screenshot", "winshell inspect screenshot needs a file.")]
    [DataRow("inspect click", "winshell inspect click needs a name.")]
    [DataRow("inspect hover", "winshell inspect hover needs a name.")]
    [DataRow("inspect record --for 1s", "winshell inspect record needs a file.")]
    public void A_command_without_the_words_it_needs_names_them(string line, string message)
    {
        var e = Assert.ThrowsExactly<ArgumentException>(() => CliParser.Parse(line.Split(' ')));

        Assert.AreEqual(message, e.Message);
    }

    [TestMethod]
    [DataRow("ipc show")]
    [DataRow("ipc call notes add")]
    [DataRow("ipc get notes count")]
    [DataRow("ipc listen notes changed")]
    [DataRow("inspect tree")]
    [DataRow("inspect screenshot out.png")]
    [DataRow("inspect click StartButton")]
    public void A_command_with_its_words_is_read(string line)
    {
        var options = CliParser.Parse(line.Split(' '));

        Assert.AreSequenceEqual(line.Split(' ')[1..], options.Positional);
    }

    [TestMethod]
    [DataRow("ipc -V")]
    [DataRow("ipc call -V")]
    [DataRow("inspect screenshot --version")]
    public void The_version_wins_over_a_command_that_lacks_words(string line)
    {
        Assert.IsTrue(CliParser.Parse(line.Split(' ')).ShowVersion);
    }

    [TestMethod]
    [DataRow("help kill", "kill")]
    [DataRow("help ipc", "ipc")]
    public void Help_with_a_command_is_that_command_s_help(string line, string command)
    {
        var options = CliParser.Parse(line.Split(' '));

        Assert.IsTrue(options.ShowHelp);
        Assert.AreEqual(command, options.Command);
    }

    [TestMethod]
    public void Help_with_a_command_and_the_version_option_is_still_help()
    {
        var options = CliParser.Parse(["help", "kill", "-V"]);

        Assert.IsTrue(options.ShowHelp);
        Assert.IsFalse(options.ShowVersion);
    }

    [TestMethod]
    public void Help_alone_is_help()
    {
        Assert.IsTrue(CliParser.Parse(["help"]).ShowHelp);
    }

    [TestMethod]
    public void Help_fills_in_the_version_and_the_config_folder()
    {
        var summary = CliParser.Help(null);
        var doctor = CliParser.Help("doctor");

        Assert.Contains(Shell.Version, summary, StringComparison.Ordinal);
        Assert.Contains(ShellLocations.Current.ConfigRoot, summary, StringComparison.Ordinal);
        Assert.Contains(ShellLocations.ConfigVariable, summary, StringComparison.Ordinal);
        Assert.Contains(ShellLocations.Current.ConfigRoot, doctor, StringComparison.Ordinal);
        Assert.DoesNotContain("{version}", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("{config-root}", doctor, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("help foo", "Unknown command \"foo\". Run winshell --help.")]
    [DataRow("help kill log", "Unexpected argument \"log\".")]
    public void Help_takes_one_command_it_knows(string line, string message)
    {
        var e = Assert.ThrowsExactly<ArgumentException>(() => CliParser.Parse(line.Split(' ')));

        Assert.AreEqual(message, e.Message);
    }
}
