namespace Winshell.ShellTests;

[TestClass]
public class WindowThumbnailPeekTests
{
    private readonly List<string> _calls = [];
    private bool _turnedOn = true;

    private WindowPeek Peek(bool succeeds = true)
    {
        return new WindowPeek(() => _turnedOn, (on, window, _) =>
        {
            _calls.Add((on ? "on " : "off ") + window);
            return succeeds;
        });
    }

    [TestMethod]
    public void Peek_shows_the_window_until_it_is_turned_off()
    {
        var peek = Peek();
        peek.Show(7, 0);
        peek.Show(0, 0);

        Assert.AreSequenceEqual(["on 7", "off 7"], _calls);
    }

    [TestMethod]
    public void Peek_needs_a_window()
    {
        var peek = Peek();
        peek.Show(0, 0);
        Assert.IsEmpty(_calls);

        peek.Show(7, 0);

        Assert.AreSequenceEqual(["on 7"], _calls);
    }

    [TestMethod]
    public void Peek_stays_off_when_the_user_turned_it_off()
    {
        _turnedOn = false;
        var peek = Peek();

        peek.Show(7, 0);
        peek.Show(0, 0);

        Assert.IsEmpty(_calls);
    }

    [TestMethod]
    public void A_setting_turned_on_later_counts_at_the_next_peek()
    {
        _turnedOn = false;
        var peek = Peek();
        peek.Show(7, 0);
        peek.Show(0, 0);

        _turnedOn = true;
        peek.Show(7, 0);

        Assert.AreSequenceEqual(["on 7"], _calls);
    }

    [TestMethod]
    public void A_peek_Windows_refused_is_not_ended()
    {
        var peek = Peek(false);

        peek.Show(7, 0);
        peek.End();

        Assert.AreSequenceEqual(["on 7"], _calls);
    }

    [TestMethod]
    public void Changing_the_window_moves_the_peek()
    {
        var peek = Peek();
        peek.Show(7, 0);
        peek.Show(8, 0);

        Assert.AreSequenceEqual(["on 7", "off 7", "on 8"], _calls);
    }

    [TestMethod]
    public void Ending_the_peek_hides_the_window()
    {
        var peek = Peek();
        peek.Show(7, 0);

        peek.End();

        Assert.AreSequenceEqual(["on 7", "off 7"], _calls);
    }
}
