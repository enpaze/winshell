using System.Runtime.InteropServices;
using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell.ShellTests;

/// <summary>
/// Fullscreen uses a real window for a moment, then puts it back. When the work area is the whole monitor
/// (the taskbar hides itself), covering the monitor is the whole check: the window's rectangle is the monitor.
/// </summary>
[Trait("Category", "Window")]
public sealed class FloatingWindowFullscreenTests
{
    [Fact]
    public async Task Fullscreen_covers_the_monitor_and_then_puts_the_frame_back()
    {
        await ShellFixture.On(_ =>
        {
            using var window = One();
            window.Visible = true;
            try
            {
                var hwnd = window.Host!.Handle;
                var before = Outer(hwnd);
                var style = Style(hwnd) & ~(uint)WINDOW_STYLE.WS_VISIBLE;

                window.Fullscreen = true;

                Assert.True(window.Fullscreen);
                Assert.False(window.Maximized);
                AssertCoversMonitor(hwnd);

                window.Fullscreen = false;

                Assert.False(window.Fullscreen);
                Assert.Equal(before, Outer(hwnd));
                Assert.Equal(style, Style(hwnd) & ~(uint)WINDOW_STYLE.WS_VISIBLE);
                AssertFramed(hwnd);
            }
            finally
            {
                window.Fullscreen = false;
            }

            return 0;
        });
    }

    [Fact]
    public async Task A_dpi_message_while_fullscreen_keeps_the_monitor_and_scales_the_old_place()
    {
        await ShellFixture.On(_ =>
        {
            using var window = One();
            window.Visible = true;
            try
            {
                var hwnd = window.Host!.Handle;
                var before = Outer(hwnd);
                var dpi = PInvoke.GetDpiForWindow((HWND)hwnd);
                Assert.NotEqual(0u, dpi);
                var nextDpi = dpi == 96 ? 192u : 96u;
                window.Fullscreen = true;
                AssertCoversMonitor(hwnd);

                var suggested = new RECT(5, 6, 7, 8);
                var memory = Marshal.AllocHGlobal(Marshal.SizeOf<RECT>());
                try
                {
                    Marshal.StructureToPtr(suggested, memory, false);
                    var wParam = (nint)(nextDpi | (nextDpi << 16));
                    PInvoke.SendMessage((HWND)hwnd, PInvoke.WM_DPICHANGED, (WPARAM)(nuint)wParam, memory);
                }
                finally
                {
                    Marshal.FreeHGlobal(memory);
                }

                Assert.True(window.Fullscreen);
                AssertCoversMonitor(hwnd);
                Assert.NotEqual(suggested, Outer(hwnd));

                window.Fullscreen = false;

                Assert.False(window.Fullscreen);
                AssertFramed(hwnd);
                var after = Outer(hwnd);
                var scale = nextDpi / (double)dpi;
                Assert.InRange(after.Width, (int)Math.Round(before.Width * scale) - 2,
                    (int)Math.Round(before.Width * scale) + 2);
                Assert.InRange(after.Height, (int)Math.Round(before.Height * scale) - 2,
                    (int)Math.Round(before.Height * scale) + 2);
            }
            finally
            {
                window.Fullscreen = false;
            }

            return 0;
        });
    }

    [Fact]
    public async Task Fullscreen_remembers_a_maximized_window()
    {
        await ShellFixture.On(_ =>
        {
            using var window = One();
            window.Visible = true;
            var hwnd = window.Host!.Handle;
            window.Maximized = true;
            Assert.True(PInvoke.IsZoomed((HWND)hwnd));
            Assert.True(window.Maximized);
            var restored = Restored(hwnd);

            window.Fullscreen = true;

            Assert.True(window.Fullscreen);
            Assert.False(window.Maximized);
            Assert.False(PInvoke.IsZoomed((HWND)hwnd));
            AssertCoversMonitor(hwnd);

            window.Fullscreen = false;

            Assert.False(window.Fullscreen);
            Assert.True(window.Maximized);
            Assert.True(PInvoke.IsZoomed((HWND)hwnd));
            AssertFramed(hwnd);
            Assert.Equal(restored, Restored(hwnd));
            return true;
        });
    }

    [Fact]
    public async Task Minimizing_a_fullscreen_window_keeps_fullscreen_and_maximized_leaves_it()
    {
        await ShellFixture.On(_ =>
        {
            using var window = One();
            window.Visible = true;
            var hwnd = window.Host!.Handle;
            var before = Outer(hwnd);

            window.Fullscreen = true;
            window.Minimized = true;
            Assert.True(window.Fullscreen);
            Assert.True(window.Minimized);
            Assert.True(PInvoke.IsIconic((HWND)hwnd));

            window.Minimized = false;
            Assert.True(window.Fullscreen);
            Assert.False(window.Minimized);
            Assert.False(PInvoke.IsIconic((HWND)hwnd));
            AssertCoversMonitor(hwnd);

            window.Maximized = true;
            Assert.False(window.Fullscreen);
            Assert.True(window.Maximized);
            Assert.True(PInvoke.IsZoomed((HWND)hwnd));
            AssertFramed(hwnd);

            window.Maximized = false;
            Assert.False(window.Fullscreen);
            Assert.Equal(before, Outer(hwnd));
            return true;
        });
    }

    [Fact]
    public async Task Fullscreen_from_a_minimized_window_shows_it_and_then_minimizes_it_again()
    {
        await ShellFixture.On(_ =>
        {
            using var window = One();
            window.Visible = true;
            var hwnd = window.Host!.Handle;
            window.Minimized = true;
            Assert.True(PInvoke.IsIconic((HWND)hwnd));

            window.Fullscreen = true;

            Assert.True(window.Fullscreen);
            Assert.False(window.Minimized);
            Assert.False(PInvoke.IsIconic((HWND)hwnd));
            AssertCoversMonitor(hwnd);

            window.Fullscreen = false;

            Assert.False(window.Fullscreen);
            Assert.True(window.Minimized);
            Assert.True(PInvoke.IsIconic((HWND)hwnd));
            AssertFramed(hwnd);
            return true;
        });
    }

    [Fact]
    public async Task An_owner_stays_under_the_window_and_can_be_changed()
    {
        await ShellFixture.On(async _ =>
        {
            using var owner = One();
            using var other = One();
            using var early = new FloatingWindow();
            early.Title = "Winshell test";
            early.Width = 320;
            early.Height = 200;
            early.Owner = owner;
            early.Visible = true;
            owner.Visible = true;
            other.Visible = true;
            using var child = new FloatingWindow();
            child.Title = "Winshell test";
            child.Width = 320;
            child.Height = 200;
            child.Owner = owner;
            child.Visible = true;
            var ownerHwnd = owner.Host!.Handle;
            var childHwnd = child.Host!.Handle;
            var otherHwnd = other.Host!.Handle;

            Assert.Equal(ownerHwnd, PInvoke.GetWindowLongPtr((HWND)childHwnd, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT));
            Assert.Equal(ownerHwnd, (nint)PInvoke.GetWindow((HWND)childHwnd, GET_WINDOW_CMD.GW_OWNER));
            Assert.Equal(ownerHwnd, PInvoke.GetWindowLongPtr((HWND)early.Host!.Handle, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT));
            Assert.Equal(0u, Style(childHwnd) & (uint)WINDOW_STYLE.WS_CHILD);
            AssertAbove(childHwnd, ownerHwnd);

            owner.Owner = child;
            Assert.Null(owner.Owner);
            child.Owner = child;
            Assert.Same(owner, child.Owner);

            child.Owner = other;
            Assert.Same(other, child.Owner);
            Assert.Equal(otherHwnd, PInvoke.GetWindowLongPtr((HWND)childHwnd, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT));
            AssertAbove(childHwnd, otherHwnd);

            child.Owner = null;
            Assert.Null(child.Owner);
            Assert.Equal(0, PInvoke.GetWindowLongPtr((HWND)childHwnd, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT));

            child.Owner = owner;
            owner.Minimized = true;
            Assert.True(PInvoke.IsIconic((HWND)ownerHwnd));
            await Wait.UntilAsync(() => !PInvoke.IsWindowVisible((HWND)childHwnd), "the owned window to hide with its owner");
            Assert.False(child.Minimized);
            owner.Minimized = false;
            Assert.False(PInvoke.IsIconic((HWND)ownerHwnd));
            await Wait.UntilAsync(() => PInvoke.IsWindowVisible((HWND)childHwnd), "the owned window to show with its owner");
            Assert.False(child.Minimized);
            return true;
        });
    }

    [Fact]
    public async Task Closing_the_owner_closes_the_owned_window_without_leaving_its_handle()
    {
        await ShellFixture.On(async _ =>
        {
            var owner = One();
            using var other = One();
            using var child = new FloatingWindow();
            child.Title = "Winshell test";
            child.Width = 320;
            child.Height = 200;
            child.Owner = owner;
            owner.Visible = true;
            other.Visible = true;
            child.Visible = true;
            var host = child.Host!;
            var hwnd = host.Handle;
            var otherHwnd = other.Host!.Handle;
            var closed = false;
            child.Closed += () => closed = true;

            owner.Dispose();

            Assert.False(PInvoke.IsWindow((HWND)hwnd));
            Assert.Equal(0, host.Handle);
            Assert.Null(child.Owner);
            Assert.True(PInvoke.IsWindow((HWND)otherHwnd));

            await Wait.UntilAsync(() => closed, "the owned window to say it closed");
            Assert.False(child.Visible);
            Assert.Null(child.Host);

            child.Visible = true;
            var again = child.Host!.Handle;
            Assert.NotEqual(hwnd, again);
            Assert.True(PInvoke.IsWindow((HWND)again));
            Assert.Equal(0, PInvoke.GetWindowLongPtr((HWND)again, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT));
            return true;
        });
    }


    private static RECT Restored(nint hwnd)
    {
        var place = new WINDOWPLACEMENT { length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>() };
        Assert.True(PInvoke.GetWindowPlacement((HWND)hwnd, ref place));
        return place.rcNormalPosition;
    }

    private static void AssertAbove(nint window, nint other)
    {
        for (var at = PInvoke.GetWindow((HWND)window, GET_WINDOW_CMD.GW_HWNDNEXT); (nint)at != 0;
             at = PInvoke.GetWindow(at, GET_WINDOW_CMD.GW_HWNDNEXT))
            if ((nint)at == other)
                return;
        Assert.Fail("The window is not above the other.");
    }

    private static FloatingWindow One()
    {
        return new FloatingWindow { Title = "Winshell test", Width = 320, Height = 200 };
    }

    private static void AssertCoversMonitor(nint hwnd)
    {
        var monitor = Screens.Of(hwnd);
        Assert.NotNull(monitor);
        var bounds = monitor.Bounds;
        var rect = Outer(hwnd);
        Assert.InRange(rect.left, bounds.left - 1, bounds.left + 1);
        Assert.InRange(rect.top, bounds.top - 1, bounds.top + 1);
        Assert.InRange(rect.right, bounds.right - 1, bounds.right + 1);
        Assert.InRange(rect.bottom, bounds.bottom - 1, bounds.bottom + 1);
        Assert.Equal(0u, Style(hwnd) & (uint)(WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_THICKFRAME));
        // A taskbar that is showing makes the work area smaller. The window then extends past it.
        if ((monitor.Work.left, monitor.Work.top, monitor.Work.right, monitor.Work.bottom) !=
            (monitor.Bounds.left, monitor.Bounds.top, monitor.Bounds.right, monitor.Bounds.bottom))
            Assert.True(
                rect.left < monitor.Work.left || rect.top < monitor.Work.top
                                              || rect.right > monitor.Work.right || rect.bottom > monitor.Work.bottom);
    }

    private static void AssertFramed(nint hwnd)
    {
        var style = Style(hwnd);
        Assert.NotEqual(0u, style & (uint)WINDOW_STYLE.WS_CAPTION);
        Assert.NotEqual(0u, style & (uint)WINDOW_STYLE.WS_THICKFRAME);
    }

    private static uint Style(nint hwnd)
    {
        return (uint)PInvoke.GetWindowLongPtr((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
    }

    private static RECT Outer(nint hwnd)
    {
        Assert.True(PInvoke.GetWindowRect((HWND)hwnd, out var rect));
        return rect;
    }
}
