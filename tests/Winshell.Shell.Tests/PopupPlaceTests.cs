using Windows.Foundation;
using Winshell.Native;

namespace Winshell.ShellTests;

[TestClass]
public sealed class PopupPlaceTests
{
    private const string First = @"\\.\DISPLAY1";
    private const string Second = @"\\.\DISPLAY2";

    private static readonly MonitorGeometry Primary =
        new(1, First, new RECT(0, 0, 1920, 1080), new RECT(0, 0, 1920, 1040), 1, true);

    private static readonly MonitorGeometry Scaled =
        new(2, Second, new RECT(1920, 0, 4480, 1440), new RECT(1920, 0, 4480, 1440), 1.5, false);

    private static readonly MonitorGeometry[] Monitors = [Primary, Scaled];

    [TestMethod]
    public void A_popup_with_nothing_set_opens_100_by_100_centered_on_the_screen_with_the_active_app_window()
    {
        var place = new PopupPlace(null, Second, PopupPosition.Center, double.NaN, double.NaN);

        var monitor = place.MonitorIn(Monitors);

        Assert.AreSame(Scaled, monitor);
        Assert.AreEqual(new RECT(3125, 645, 3275, 795), place.BoundsOn(monitor!));
    }

    [TestMethod]
    public void A_popup_with_nothing_set_opens_on_the_primary_screen_when_no_app_window_is_active()
    {
        var place = new PopupPlace(null, null, PopupPosition.Center, double.NaN, double.NaN);

        var monitor = place.MonitorIn(Monitors);

        Assert.AreSame(Primary, monitor);
        Assert.AreEqual(new RECT(910, 470, 1010, 570), place.BoundsOn(monitor!));
    }

    [TestMethod]
    public void A_popup_opens_on_the_primary_screen_when_the_active_app_windows_screen_is_gone()
    {
        var place = new PopupPlace(null, @"\\.\DISPLAY9", PopupPosition.Top, double.NaN, double.NaN);

        Assert.AreSame(Primary, place.MonitorIn(Monitors));
    }

    [TestMethod]
    public void A_popups_screen_wins_over_the_screen_with_the_active_app_window()
    {
        var place = new PopupPlace(First, Second, PopupPosition.Center, 200, 100);

        var monitor = place.MonitorIn(Monitors);

        Assert.AreSame(Primary, monitor);
        Assert.AreEqual(new RECT(860, 470, 1060, 570), place.BoundsOn(monitor!));
    }

    [TestMethod]
    public void A_popup_at_a_point_opens_on_the_screen_under_it_whatever_app_window_is_active()
    {
        var place = new PopupPlace(null, First, PopupPosition.At(new Point(2000, 10)), 200, 100);

        Assert.AreSame(Scaled, place.MonitorIn(Monitors));
    }

    [TestMethod]
    public void A_popup_keeps_its_screen_while_shown_and_follows_the_active_app_window_when_reopened()
    {
        var opened = PopupWindow.ShownOn(true, null, [Screen(Second), Screen(First)]);
        var whileShown = PopupWindow.ShownOn(false, opened, [Screen(First)]);
        var reopened = PopupWindow.ShownOn(true, whileShown, [Screen(First)]);
        var reopenedWithNoAppWindow = PopupWindow.ShownOn(true, reopened, null);

        Assert.AreEqual(Second, opened);
        Assert.AreEqual(Second, whileShown);
        Assert.AreEqual(First, reopened);
        Assert.IsNull(reopenedWithNoAppWindow);
    }

    private static ScreenInfo Screen(string id)
    {
        return new ScreenInfo(id, id, "", "", "", default, default, 1, 0, 0, 60, id == First);
    }
}
