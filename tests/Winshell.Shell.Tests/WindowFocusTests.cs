namespace Winshell.ShellTests;

[TestClass]
public class WindowFocusTests
{
    private sealed class FakeWindow : IFocusEffects
    {
        private bool? _activation;
        private bool _grabbing;

        private List<string> Effects { get; } = [];

        public void AllowActivation(bool allow)
        {
            if (_activation == allow)
                return;
            _activation = allow;
            Effects.Add(allow ? "activation allowed" : "activation refused");
        }

        public void Show(bool activate)
        {
            Effects.Add(activate ? "show activating" : "show without activating");
        }

        public void TakeForeground()
        {
            Effects.Add("foreground");
        }

        public void TakeKeyboard()
        {
            Effects.Add("keyboard");
        }

        public void GrabOutsideClicks(bool on)
        {
            if (_grabbing == on)
                return;
            _grabbing = on;
            Effects.Add(on ? "grab clicks" : "release clicks");
        }

        public string[] Drain()
        {
            var seen = Effects.ToArray();
            Effects.Clear();
            return seen;
        }
    }

    private static (WindowFocus Focus, FakeWindow Window) Of(WindowKind kind)
    {
        var window = new FakeWindow();
        return (new WindowFocus(kind, window), window);
    }

    [TestMethod]
    public void A_floating_window_always_takes_the_keyboard()
    {
        var (focus, window) = Of(WindowKind.Floating);

        focus.Show();
        focus.TakeKeyboard();
        focus.Release();

        Assert.AreSequenceEqual(["show activating", "keyboard"], window.Drain());
        Assert.IsTrue(focus.ActivatesOnClick);
        Assert.IsFalse(focus.DismissesOnEscape);
    }

    [TestMethod]
    [DataRow(true, "activation allowed", true)]
    [DataRow(false, "activation refused", false)]
    public void A_panel_takes_the_keyboard_only_while_focusable(bool focusable, string activation, bool takes)
    {
        var (focus, window) = Of(WindowKind.Panel);

        focus.Arm(focusable);
        focus.Show();
        focus.TakeKeyboard();

        Assert.AreSequenceEqual(
            takes ? [activation, "show without activating", "keyboard"] : [activation, "show without activating"],
            window.Drain());
        Assert.AreEqual(takes, focus.ActivatesOnClick);
        Assert.IsFalse(focus.DismissesOnEscape);
    }

    [TestMethod]
    public void A_panel_that_stops_being_focusable_refuses_the_keyboard()
    {
        var (focus, window) = Of(WindowKind.Panel);

        focus.Arm(true);
        focus.Arm(false);
        window.Drain();
        focus.TakeKeyboard();

        Assert.IsEmpty(window.Drain());
    }

    [TestMethod]
    public void A_popup_that_does_not_grab_focus_shows_without_activating_and_ignores_clicks_and_keys()
    {
        var (focus, window) = Of(WindowKind.Popup);

        focus.Arm(false);
        focus.Show();
        focus.TakeKeyboard();

        Assert.AreSequenceEqual(["activation refused", "show without activating"], window.Drain());
        Assert.IsFalse(focus.ActivatesOnClick);
        Assert.IsFalse(focus.DismissesOnEscape);
    }

    [TestMethod]
    public void A_popup_that_grabs_focus_shows_activating_takes_the_foreground_then_grabs_clicks()
    {
        var (focus, window) = Of(WindowKind.Popup);

        focus.Arm(true);
        focus.Show();
        focus.TakeKeyboard();

        Assert.AreSequenceEqual(["activation allowed", "show activating", "foreground", "grab clicks", "keyboard"],
            window.Drain());
        Assert.IsTrue(focus.ActivatesOnClick);
        Assert.IsTrue(focus.DismissesOnEscape);
    }

    [TestMethod]
    public void A_popup_that_grabs_focus_stops_grabbing_clicks_when_it_hides_and_grabs_again_when_shown()
    {
        var (focus, window) = Of(WindowKind.Popup);
        focus.Arm(true);
        focus.Show();
        window.Drain();

        focus.Release();
        Assert.AreSequenceEqual(["release clicks"], window.Drain());

        focus.Show();
        Assert.AreSequenceEqual(["show activating", "foreground", "grab clicks"], window.Drain());
    }

    [TestMethod]
    public void A_popup_that_grabs_focus_grabs_clicks_only_while_shown()
    {
        var (focus, window) = Of(WindowKind.Popup);

        focus.Arm(true);
        Assert.AreSequenceEqual(["activation allowed"], window.Drain());

        focus.Show();
        window.Drain();
        focus.Arm(true);
        Assert.IsEmpty(window.Drain());
    }

    [TestMethod]
    public void A_shown_popup_that_stops_grabbing_focus_releases_clicks_and_refuses_the_keyboard()
    {
        var (focus, window) = Of(WindowKind.Popup);
        focus.Arm(true);
        focus.Show();
        window.Drain();

        focus.Arm(false);
        focus.TakeKeyboard();

        Assert.AreSequenceEqual(["activation refused", "release clicks"], window.Drain());
        Assert.IsFalse(focus.ActivatesOnClick);
        Assert.IsFalse(focus.DismissesOnEscape);
    }
}
