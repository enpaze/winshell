namespace Winshell.Tests;

[TestClass]
public class InspectInputOptionTests
{
    private static Invocation Parse(params string[] args)
    {
        return CliParser.Parse(args);
    }

    [TestMethod]
    public void A_right_press_is_read_wherever_the_flag_is()
    {
        Assert.IsTrue(Parse("inspect", "press", "--right", "Menu", "PanelView").IsSet(CliFlags.Right));
        Assert.IsTrue(Parse("inspect", "press", "Menu", "PanelView", "--right").IsSet(CliFlags.Right));
        Assert.AreSequenceEqual<string>(["press", "Menu", "PanelView"],
            Parse("inspect", "--right", "press", "Menu", "PanelView").Positional);
    }

    [TestMethod]
    public void A_negative_number_on_click_is_an_unknown_option()
    {
        var error = Assert.ThrowsExactly<ArgumentException>(() => Parse("inspect", "click", "-3"));

        Assert.AreEqual("Unknown option \"-3\".", error.Message);
    }

    [TestMethod]
    public void Steps_up_are_a_negative_number_and_not_an_option()
    {
        Assert.AreSequenceEqual<string>(["scroll", "List", "-3", "PanelView"],
            Parse("inspect", "scroll", "List", "-3", "PanelView").Positional);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("down")]
    [DataRow("101")]
    [DataRow("-101")]
    public void Steps_are_a_whole_number_from_1_to_100_either_way(string steps)
    {
        var e = Assert.ThrowsExactly<ArgumentException>(() => Parse("inspect", "scroll", "List", steps));

        Assert.AreEqual("winshell inspect scroll needs a number of steps from 1 to 100, or -1 to -100 to scroll up.",
            e.Message);
    }

    [TestMethod]
    [DataRow("Win+D",
        "winshell inspect key does not press Win+D: Windows itself takes the Windows key, not the shell.")]
    [DataRow("Tab, Win",
        "winshell inspect key does not press Win: Windows itself takes the Windows key, not the shell.")]
    public void The_windows_key_is_refused(string keys, string message)
    {
        var e = Assert.ThrowsExactly<ArgumentException>(() => Parse("inspect", "key", keys));

        Assert.AreEqual(message, e.Message);
    }

    [TestMethod]
    public void Keys_it_can_t_read_are_refused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Parse("inspect", "key", "Ctrl+Nope"));
    }
}
