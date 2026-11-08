namespace Winshell.Tests;

/// <summary>
/// What <c>winshell inspect</c>'s real input commands read from the command line, and what they refuse before any
/// instance is called. These stay inside <see cref="CliParser"/> so a live instance on this PC is never called.
/// </summary>
public class InspectInputOptionTests
{
    private static CliOptions Parse(params string[] args)
    {
        return CliParser.Parse(args);
    }

    [Fact]
    public void A_right_press_is_read_wherever_the_flag_is()
    {
        Assert.True(Parse("inspect", "press", "--right", "Menu", "PanelView").Right);
        Assert.True(Parse("inspect", "press", "Menu", "PanelView", "--right").Right);
        Assert.Equal(["press", "Menu", "PanelView"],
            Parse("inspect", "--right", "press", "Menu", "PanelView").Positional);
    }

    [Fact]
    public void Steps_up_are_a_negative_number_and_not_an_option()
    {
        Assert.Equal(["scroll", "List", "-3", "PanelView"],
            Parse("inspect", "scroll", "List", "-3", "PanelView").Positional);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("down")]
    [InlineData("101")]
    [InlineData("-101")]
    public void Steps_are_a_whole_number_from_1_to_100_either_way(string steps)
    {
        var e = Assert.Throws<ArgumentException>(() => Parse("inspect", "scroll", "List", steps));

        Assert.Equal("winshell inspect scroll needs a number of steps from 1 to 100, or -1 to -100 to scroll up.",
            e.Message);
    }

    [Theory]
    [InlineData("Win+D",
        "winshell inspect key does not press Win+D: Windows itself takes the Windows key, not the shell.")]
    [InlineData("Tab, Win",
        "winshell inspect key does not press Win: Windows itself takes the Windows key, not the shell.")]
    public void The_windows_key_is_refused(string keys, string message)
    {
        var e = Assert.Throws<ArgumentException>(() => Parse("inspect", "key", keys));

        Assert.Equal(message, e.Message);
    }

    [Fact]
    public void Keys_it_can_t_read_are_refused()
    {
        Assert.Throws<ArgumentException>(() => Parse("inspect", "key", "Ctrl+Nope"));
    }
}
