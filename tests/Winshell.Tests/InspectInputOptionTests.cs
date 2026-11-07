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
    [InlineData(new[] { "inspect", "point", "Menu", "--right" }, "winshell inspect point does not take \"--right\".")]
    [InlineData(new[] { "inspect", "press", "Menu", "--fps", "30" }, "winshell inspect press does not take \"--fps\".")]
    [InlineData(new[] { "inspect", "point" }, "winshell inspect point needs a name.")]
    [InlineData(new[] { "inspect", "scroll", "List" }, "winshell inspect scroll needs a name and a number of steps.")]
    [InlineData(new[] { "inspect", "scroll", "List", "0" },
        "winshell inspect scroll needs a number of steps from 1 to 100, or -1 to -100 to scroll up.")]
    [InlineData(new[] { "inspect", "scroll", "List", "down" },
        "winshell inspect scroll needs a number of steps from 1 to 100, or -1 to -100 to scroll up.")]
    [InlineData(new[] { "inspect", "scroll", "List", "101" },
        "winshell inspect scroll needs a number of steps from 1 to 100, or -1 to -100 to scroll up.")]
    [InlineData(new[] { "inspect", "key" }, "winshell inspect key needs keys.")]
    [InlineData(new[] { "inspect", "key", "Ctrl+Nope" },
        "Could not read \"Ctrl+Nope\" as a Windows key chord. Unknown key \"Nope\". Use Win, Ctrl, Alt and Shift, as in Ctrl+Shift+K or Win.")]
    [InlineData(new[] { "inspect", "key", "Win+D" },
        "winshell inspect key does not press Win+D: Windows itself takes the Windows key, not the shell.")]
    [InlineData(new[] { "inspect", "key", "Tab, Win" },
        "winshell inspect key does not press Win: Windows itself takes the Windows key, not the shell.")]
    public void Input_that_can_t_be_sent_is_refused_before_the_shell_is_asked(string[] args, string message)
    {
        var e = Assert.Throws<ArgumentException>(() => Parse(args));

        Assert.Equal(message, e.Message);
    }
}
