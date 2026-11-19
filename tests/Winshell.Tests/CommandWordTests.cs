namespace Winshell.Tests;

public class CommandWordTests
{
    [Theory]
    [InlineData("foo bar", "foo")]
    [InlineData("-v foo bar", "foo")]
    [InlineData("msg notes add", "msg")]
    [InlineData("foo --help", "foo")]
    [InlineData("widget install enpaze/widgets", "widget")]
    public void An_unknown_command_is_named_even_with_words_after_it(string line, string word)
    {
        var e = Assert.Throws<ArgumentException>(() => CliParser.Parse(line.Split(' ')));

        Assert.Equal($"Unknown command \"{word}\". Run winshell --help.", e.Message);
    }

    [Theory]
    [InlineData("ipc bogus", "Unknown ipc command \"bogus\". Run winshell ipc --help.")]
    [InlineData("ipc bogus target func", "Unknown ipc command \"bogus\". Run winshell ipc --help.")]
    [InlineData("ipc prop get notes count", "Unknown ipc command \"prop\". Run winshell ipc --help.")]
    [InlineData("inspect bogus", "Unknown inspect command \"bogus\". Run winshell inspect --help.")]
    public void An_unknown_subcommand_is_named(string line, string message)
    {
        var e = Assert.Throws<ArgumentException>(() => CliParser.Parse(line.Split(' ')));

        Assert.Equal(message, e.Message);
    }

    [Theory]
    [InlineData("ipc", "winshell ipc needs a command. Run winshell ipc --help.")]
    [InlineData("inspect", "winshell inspect needs a command. Run winshell inspect --help.")]
    [InlineData("ipc call", "winshell ipc call needs a target and a function.")]
    [InlineData("ipc call notes", "winshell ipc call needs a target and a function.")]
    [InlineData("ipc get", "winshell ipc get needs a target and a property.")]
    [InlineData("ipc get notes", "winshell ipc get needs a target and a property.")]
    [InlineData("ipc wait notes", "winshell ipc wait needs a target and a signal.")]
    [InlineData("ipc listen", "winshell ipc listen needs a target and a signal.")]
    [InlineData("inspect screenshot", "winshell inspect screenshot needs a file.")]
    [InlineData("inspect click", "winshell inspect click needs a name.")]
    [InlineData("inspect hover", "winshell inspect hover needs a name.")]
    [InlineData("inspect record --for 1s", "winshell inspect record needs a file.")]
    public void A_command_without_the_words_it_needs_names_them(string line, string message)
    {
        var e = Assert.Throws<ArgumentException>(() => CliParser.Parse(line.Split(' ')));

        Assert.Equal(message, e.Message);
    }

    [Theory]
    [InlineData("ipc show")]
    [InlineData("ipc call notes add")]
    [InlineData("ipc get notes count")]
    [InlineData("ipc listen notes changed")]
    [InlineData("inspect tree")]
    [InlineData("inspect screenshot out.png")]
    [InlineData("inspect click StartButton")]
    public void A_command_with_its_words_is_read(string line)
    {
        var options = CliParser.Parse(line.Split(' '));

        Assert.Equal(line.Split(' ')[1..], options.Positional);
    }

    [Theory]
    [InlineData("ipc --version")]
    [InlineData("ipc call --version")]
    [InlineData("inspect screenshot --version")]
    public void The_version_wins_over_a_command_that_lacks_words(string line)
    {
        Assert.True(CliParser.Parse(line.Split(' ')).ShowVersion);
    }

    [Theory]
    [InlineData("help stop", "stop")]
    [InlineData("help ipc", "ipc")]
    public void Help_with_a_command_is_that_command_s_help(string line, string command)
    {
        var options = CliParser.Parse(line.Split(' '));

        Assert.True(options.ShowHelp);
        Assert.Equal(command, options.Command);
    }

    [Fact]
    public void Help_with_a_command_and_the_version_option_is_still_help()
    {
        var options = CliParser.Parse(["help", "stop", "--version"]);

        Assert.True(options.ShowHelp);
        Assert.False(options.ShowVersion);
    }

    [Fact]
    public void Help_alone_is_help()
    {
        Assert.True(CliParser.Parse(["help"]).ShowHelp);
    }

    [Fact]
    public void Help_fills_in_the_version_and_the_config_folder()
    {
        var summary = CliParser.Help(null);
        var doctor = CliParser.Help("doctor");

        Assert.Contains(Shell.Version, summary, StringComparison.Ordinal);
        Assert.Contains(ShellLocations.Current.ShellsRoot, summary, StringComparison.Ordinal);
        Assert.Contains(ShellLocations.ShellsVariable, summary, StringComparison.Ordinal);
        Assert.Contains(ShellLocations.Current.ShellsRoot, doctor, StringComparison.Ordinal);
        Assert.DoesNotContain("{version}", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("{shells-root}", doctor, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("help foo", "Unknown command \"foo\". Run winshell --help.")]
    [InlineData("help stop log", "Unexpected argument \"log\".")]
    public void Help_takes_one_command_it_knows(string line, string message)
    {
        var e = Assert.Throws<ArgumentException>(() => CliParser.Parse(line.Split(' ')));

        Assert.Equal(message, e.Message);
    }
}
