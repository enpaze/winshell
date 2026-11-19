using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Winshell.Native;

namespace Winshell.ShellTests;

[TestClass]
[TestCategory("Window")]
public sealed class WindowKindTests
{
    [TestMethod]
    [DataRow("panel", true)]
    [DataRow("floating", true)]
    [DataRow("popup", false)]
    public async Task A_window_shows_nothing_until_it_is_added_and_a_panel_or_floating_window_then_shows(string kind,
        bool shown)
    {
        var (visibleBefore, hostBefore, visibleAfter, shownAfter) = await ShellFixture.On(shell =>
        {
            using ShellWindow window = kind switch
            {
                "panel" => new PanelWindow { Edge = Edge.Top, Align = PanelAlign.Start, Width = 200, Height = 48, Content = new Grid() },
                "popup" => new PopupWindow { Width = 200, Height = 120, Content = new Grid() },
                _ => new FloatingWindow { Title = "Winshell test", Width = 300, Height = 200, Content = new Grid() }
            };
            var visible = window.Visible;
            var host = window.Host is not null;
            shell.Add(window);
            return (visible, host, window.Visible, window.Host?.Place.IsVisible ?? false);
        });

        Assert.AreEqual(shown, visibleBefore);
        Assert.IsFalse(hostBefore);
        Assert.AreEqual(shown, visibleAfter);
        Assert.AreEqual(shown, shownAfter);
    }

    [TestMethod]
    public async Task A_window_whose_add_failed_leaves_no_window_behind_and_shows_when_it_is_added_again()
    {
        var (hostAfterFailure, shownAfterRetry) = await ShellFixture.On(shell =>
        {
            var taken = new Grid();
            new Grid().Children.Add(taken);
            using var panel = new PanelWindow { Edge = Edge.Top, Align = PanelAlign.Start, Width = 200, Height = 48, Content = taken };
            Assert.Throws<Exception>(() => shell.Add(panel));
            var host = panel.Host is not null;
            panel.Content = new Grid();
            shell.Add(panel);
            return (host, panel.Host?.Place.IsVisible ?? false);
        });

        Assert.IsFalse(hostAfterFailure);
        Assert.IsTrue(shownAfterRetry);
    }

    [TestMethod]
    public async Task A_popup_sits_where_it_was_asked()
    {
        var rect = await ShellFixture.On(async shell =>
        {
            var screen = shell.Screens[0].Geometry;
            var content = new Grid();
            using var popup = shell.Add(new PopupWindow());
            popup.Position = PopupPosition.At(new Point(screen.X + 60, screen.Y + 60));
            popup.Width = 200;
            popup.Height = 120;
            popup.Content = content;
            popup.Visible = true;
            await ShellFixture.LaidOut(content);
            PInvoke.GetWindowRect((HWND)popup.Host!.Handle, out var r);
            return (r.Width, r.Height, popup.Scale);
        });

        Assert.AreEqual(((int)(200 * rect.Scale), (int)(120 * rect.Scale)), (rect.Width, rect.Height));
    }

    [TestMethod]
    public async Task A_popup_that_grabs_focus_is_dismissed_by_a_click_outside_it()
    {
        var dismissed = await ShellFixture.On(async shell =>
        {
            var screen = shell.Screens[0].Geometry;
            using var popup = shell.Add(new PopupWindow());
            popup.Position = PopupPosition.At(new Point(screen.X + 60, screen.Y + 60));
            popup.Width = 200;
            popup.Height = 120;
            popup.GrabFocus = true;
            popup.Content = new Grid();
            var heard = 0;
            popup.Dismissed += () => heard++;
            popup.Visible = true;
            await Task.Yield();

            shell.Platform.OutsideClicks.Click(new POINT { X = (int)screen.X + 1000, Y = (int)screen.Y + 500 });

            return (heard, popup.Visible);
        });

        Assert.AreEqual((1, false), dismissed);
    }

    [TestMethod]
    public async Task A_click_inside_a_popup_that_grabs_focus_leaves_it_open()
    {
        var heard = await ShellFixture.On(async shell =>
        {
            var screen = shell.Screens[0].Geometry;
            using var popup = shell.Add(new PopupWindow());
            popup.Position = PopupPosition.At(new Point(screen.X + 60, screen.Y + 60));
            popup.Width = 200;
            popup.Height = 120;
            popup.GrabFocus = true;
            popup.Content = new Grid();
            var count = 0;
            popup.Dismissed += () => count++;
            popup.Visible = true;
            await Task.Yield();
            PInvoke.GetWindowRect((HWND)popup.Host!.Handle, out var r);

            shell.Platform.OutsideClicks.Click(new POINT { X = r.left + 5, Y = r.top + 5 });

            return count;
        });

        Assert.AreEqual(0, heard);
    }

    [TestMethod]
    public async Task A_click_on_a_flyout_that_extends_past_a_grabbing_popup_leaves_the_popup_open()
    {
        var after = await ShellFixture.On(async shell =>
        {
            var screen = shell.Screens[0].Geometry;
            var button = new Button { Content = "Open", Width = 80, Height = 32 };
            var flyout = new Flyout
            {
                Content = new TextBlock { Text = "Flyout", Height = 300 },
                ShouldConstrainToRootBounds = false,
                Placement = FlyoutPlacementMode.Bottom
            };
            var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            flyout.Opened += (_, _) => opened.TrySetResult();
            using var popup = shell.Add(new PopupWindow());
            popup.Position = PopupPosition.At(new Point(screen.X + 60, screen.Y + 60));
            popup.Width = 200;
            popup.Height = 48;
            popup.GrabFocus = true;
            popup.Content = button;
            var heard = 0;
            popup.Dismissed += () => heard++;
            popup.Visible = true;
            await ShellFixture.LaidOut(button);
            flyout.ShowAt(button);
            await opened.Task;
            var child = (FrameworkElement)VisualTreeHelper.GetOpenPopupsForXamlRoot(button.XamlRoot)[0].Child;
            var corner = child.TransformToVisual(null).TransformPoint(default);
            var (left, _) = popup.Host!.Place.MapToScreen(corner.X, corner.Y);
            PInvoke.GetWindowRect((HWND)popup.Host.Handle, out var r);

            var below = new POINT { X = left + 10, Y = r.bottom + 10 };
            shell.Platform.OutsideClicks.Click(below);
            var result = (heard, popup.Visible, flyout.IsOpen);

            shell.Platform.OutsideClicks.Click(new POINT { X = (int)screen.Right - 5, Y = (int)screen.Bottom - 5 });
            return result;
        });

        Assert.AreEqual((0, true, true), after);
    }

    [TestMethod]
    public async Task Focus_on_a_popup_that_does_not_grab_focus_leaves_the_app_in_front_active()
    {
        var (app, _, foreground) = await FocusInFrontOfApp(shell => Popup(shell, false));

        Assert.AreEqual(app, foreground);
    }

    [TestMethod]
    public async Task Focus_on_a_popup_that_grabs_focus_takes_the_foreground()
    {
        var (_, popup, foreground) = await FocusInFrontOfApp(shell => Popup(shell, true));

        Assert.AreEqual(popup, foreground);
    }

    [TestMethod]
    public async Task Focus_on_a_panel_that_is_not_focusable_leaves_the_app_in_front_active()
    {
        var (app, _, foreground) = await FocusInFrontOfApp(_ => new PanelWindow
        {
            Edge = Edge.Top,
            Align = PanelAlign.Start,
            Margins = new Thickness(40, 40, 0, 0),
            Width = 200,
            Height = 48,
            Focusable = false,
            Content = new Grid()
        });

        Assert.AreEqual(app, foreground);
    }

    private static PopupWindow Popup(Shell shell, bool grabFocus)
    {
        var screen = shell.Screens[0].Geometry;
        return new PopupWindow
        {
            Position = PopupPosition.At(new Point(screen.X + 60, screen.Y + 60)),
            Width = 200,
            Height = 120,
            GrabFocus = grabFocus,
            Content = new Grid()
        };
    }

    private static Task<(nint App, nint Window, nint Foreground)> FocusInFrontOfApp(Func<Shell, ShellWindow> create)
    {
        return ShellFixture.On(async shell =>
        {
            using var window = shell.Add(create(shell));
            window.Visible = true;
            using var app = new FloatingWindow();
            app.Width = 300;
            app.Height = 200;
            app.Content = new Grid();
            shell.Add(app);
            await Task.Yield();
            var front = app.Host!.Handle;
            app.Focus();
            await Wait.UntilAsync(() => (nint)PInvoke.GetForegroundWindow() == front, "the app window to come to the front");

            window.Focus();

            return (front, window.Host!.Handle, (nint)PInvoke.GetForegroundWindow());
        });
    }

    [TestMethod]
    public async Task A_floating_window_has_the_size_and_title_it_was_given()
    {
        var read = await ShellFixture.On(async shell =>
        {
            var content = new Grid();
            using var window = new FloatingWindow();
            window.Title = "Notes";
            window.Width = 500;
            window.Height = 300;
            window.Content = content;
            shell.Add(window);
            await ShellFixture.LaidOut(content);
            return (window.ActualWidth, window.ActualHeight, Win32Title(window.Host!.Handle));
        });

        Assert.AreEqual((500d, 300d, "Notes"), read);
    }

    [TestMethod]
    public async Task A_flyout_on_a_panel_that_never_activates_closes_on_a_click_outside_it()
    {
        var after = await ShellFixture.On(async shell =>
        {
            var button = new Button { Content = "Open", Width = 80, Height = 32 };
            var flyout = new Flyout
            {
                Content = new TextBlock { Text = "Flyout" },
                ShouldConstrainToRootBounds = false,
                Placement = FlyoutPlacementMode.Bottom
            };
            var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            flyout.Opened += (_, _) => opened.TrySetResult();
            using var panel = new PanelWindow();
            panel.Edge = Edge.Top;
            panel.Align = PanelAlign.Start;
            panel.Margins = new Thickness(40, 40, 0, 0);
            panel.Width = 200;
            panel.Height = 48;
            panel.Content = button;
            shell.Add(panel);
            await ShellFixture.LaidOut(button);
            flyout.ShowAt(button);
            await opened.Task;
            var open = VisualTreeHelper.GetOpenPopupsForXamlRoot(button.XamlRoot).Count;

            var far = shell.Screens[0].Geometry;
            shell.Platform.OutsideClicks.Click(new POINT { X = (int)far.Right - 5, Y = (int)far.Bottom - 5 });

            return (open, flyout.IsOpen);
        });

        Assert.AreEqual((1, false), after);
    }

    private static string Win32Title(nint hwnd)
    {
        Span<char> text = stackalloc char[64];
        var length = PInvoke.GetWindowText((HWND)hwnd, text);
        return new string(text[..length]);
    }
}
