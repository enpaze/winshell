using System.Runtime.InteropServices;
using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell.ShellTests;

[TestClass]
[TestCategory("Window")]
public sealed class FloatingWindowFullscreenTests
{
    [TestMethod]
    public async Task Fullscreen_covers_the_monitor_and_then_puts_the_frame_back()
    {
        await ShellFixture.On(shell =>
        {
            using var window = One();
            shell.Add(window);
            try
            {
                var hwnd = window.Host!.Handle;
                var before = Outer(hwnd);
                var style = Style(hwnd) & ~(uint)WINDOW_STYLE.WS_VISIBLE;

                window.Fullscreen = true;

                Assert.IsTrue(window.Fullscreen);
                Assert.IsFalse(window.Maximized);
                AssertCoversMonitor(hwnd);

                window.Fullscreen = false;

                Assert.IsFalse(window.Fullscreen);
                Assert.AreEqual(before, Outer(hwnd));
                Assert.AreEqual(style, Style(hwnd) & ~(uint)WINDOW_STYLE.WS_VISIBLE);
                AssertFramed(hwnd);
            }
            finally
            {
                window.Fullscreen = false;
            }

            return 0;
        });
    }

    [TestMethod]
    public async Task A_dpi_message_while_fullscreen_keeps_the_monitor_and_scales_the_old_place()
    {
        await ShellFixture.On(shell =>
        {
            using var window = One();
            shell.Add(window);
            try
            {
                var hwnd = window.Host!.Handle;
                var before = Outer(hwnd);
                var dpi = PInvoke.GetDpiForWindow((HWND)hwnd);
                Assert.AreNotEqual(0u, dpi);
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

                Assert.IsTrue(window.Fullscreen);
                AssertCoversMonitor(hwnd);
                Assert.AreNotEqual(suggested, Outer(hwnd));

                window.Fullscreen = false;

                Assert.IsFalse(window.Fullscreen);
                AssertFramed(hwnd);
                var after = Outer(hwnd);
                var scale = nextDpi / (double)dpi;
                Assert.IsInRange((int)Math.Round(before.Width * scale) - 2, (int)Math.Round(before.Width * scale) + 2,
                    after.Width);
                Assert.IsInRange((int)Math.Round(before.Height * scale) - 2, (int)Math.Round(before.Height * scale) + 2,
                    after.Height);
            }
            finally
            {
                window.Fullscreen = false;
            }

            return 0;
        });
    }

    [TestMethod]
    public async Task Fullscreen_remembers_a_maximized_window()
    {
        await ShellFixture.On(shell =>
        {
            using var window = One();
            shell.Add(window);
            var hwnd = window.Host!.Handle;
            window.Maximized = true;
            Assert.IsTrue(PInvoke.IsZoomed((HWND)hwnd));
            Assert.IsTrue(window.Maximized);
            var restored = Restored(hwnd);

            window.Fullscreen = true;

            Assert.IsTrue(window.Fullscreen);
            Assert.IsFalse(window.Maximized);
            Assert.IsFalse(PInvoke.IsZoomed((HWND)hwnd));
            AssertCoversMonitor(hwnd);

            window.Fullscreen = false;

            Assert.IsFalse(window.Fullscreen);
            Assert.IsTrue(window.Maximized);
            Assert.IsTrue(PInvoke.IsZoomed((HWND)hwnd));
            AssertFramed(hwnd);
            Assert.AreEqual(restored, Restored(hwnd));
            return true;
        });
    }

    [TestMethod]
    public async Task Minimizing_a_fullscreen_window_keeps_fullscreen_and_maximized_leaves_it()
    {
        await ShellFixture.On(shell =>
        {
            using var window = One();
            shell.Add(window);
            var hwnd = window.Host!.Handle;
            var before = Outer(hwnd);

            window.Fullscreen = true;
            window.Minimized = true;
            Assert.IsTrue(window.Fullscreen);
            Assert.IsTrue(window.Minimized);
            Assert.IsTrue(PInvoke.IsIconic((HWND)hwnd));

            window.Minimized = false;
            Assert.IsTrue(window.Fullscreen);
            Assert.IsFalse(window.Minimized);
            Assert.IsFalse(PInvoke.IsIconic((HWND)hwnd));
            AssertCoversMonitor(hwnd);

            window.Maximized = true;
            Assert.IsFalse(window.Fullscreen);
            Assert.IsTrue(window.Maximized);
            Assert.IsTrue(PInvoke.IsZoomed((HWND)hwnd));
            AssertFramed(hwnd);

            window.Maximized = false;
            Assert.IsFalse(window.Fullscreen);
            Assert.AreEqual(before, Outer(hwnd));
            return true;
        });
    }

    [TestMethod]
    public async Task Fullscreen_from_a_minimized_window_shows_it_and_then_minimizes_it_again()
    {
        await ShellFixture.On(shell =>
        {
            using var window = One();
            shell.Add(window);
            var hwnd = window.Host!.Handle;
            window.Minimized = true;
            Assert.IsTrue(PInvoke.IsIconic((HWND)hwnd));

            window.Fullscreen = true;

            Assert.IsTrue(window.Fullscreen);
            Assert.IsFalse(window.Minimized);
            Assert.IsFalse(PInvoke.IsIconic((HWND)hwnd));
            AssertCoversMonitor(hwnd);

            window.Fullscreen = false;

            Assert.IsFalse(window.Fullscreen);
            Assert.IsTrue(window.Minimized);
            Assert.IsTrue(PInvoke.IsIconic((HWND)hwnd));
            AssertFramed(hwnd);
            return true;
        });
    }

    [TestMethod]
    public async Task An_owner_stays_under_the_window_and_can_be_changed()
    {
        await ShellFixture.On(async shell =>
        {
            using var owner = One();
            using var other = One();
            using var early = new FloatingWindow();
            early.Title = "Winshell test";
            early.Width = 320;
            early.Height = 200;
            early.Owner = owner;
            shell.Add(early);
            shell.Add(owner);
            shell.Add(other);
            using var child = new FloatingWindow();
            child.Title = "Winshell test";
            child.Width = 320;
            child.Height = 200;
            child.Owner = owner;
            shell.Add(child);
            var ownerHwnd = owner.Host!.Handle;
            var childHwnd = child.Host!.Handle;
            var otherHwnd = other.Host!.Handle;

            Assert.AreEqual(ownerHwnd, PInvoke.GetWindowLongPtr((HWND)childHwnd, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT));
            Assert.AreEqual(ownerHwnd, (nint)PInvoke.GetWindow((HWND)childHwnd, GET_WINDOW_CMD.GW_OWNER));
            Assert.AreEqual(ownerHwnd, PInvoke.GetWindowLongPtr((HWND)early.Host!.Handle, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT));
            Assert.AreEqual(0u, Style(childHwnd) & (uint)WINDOW_STYLE.WS_CHILD);
            AssertAbove(childHwnd, ownerHwnd);

            owner.Owner = child;
            Assert.IsNull(owner.Owner);
            child.Owner = child;
            Assert.AreSame(owner, child.Owner);

            child.Owner = other;
            Assert.AreSame(other, child.Owner);
            Assert.AreEqual(otherHwnd, PInvoke.GetWindowLongPtr((HWND)childHwnd, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT));
            AssertAbove(childHwnd, otherHwnd);

            child.Owner = null;
            Assert.IsNull(child.Owner);
            Assert.AreEqual(0, PInvoke.GetWindowLongPtr((HWND)childHwnd, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT));

            child.Owner = owner;
            owner.Minimized = true;
            Assert.IsTrue(PInvoke.IsIconic((HWND)ownerHwnd));
            await Wait.UntilAsync(() => !PInvoke.IsWindowVisible((HWND)childHwnd), "the owned window to hide with its owner");
            Assert.IsFalse(child.Minimized);
            owner.Minimized = false;
            Assert.IsFalse(PInvoke.IsIconic((HWND)ownerHwnd));
            await Wait.UntilAsync(() => PInvoke.IsWindowVisible((HWND)childHwnd), "the owned window to show with its owner");
            Assert.IsFalse(child.Minimized);
            return true;
        });
    }

    [TestMethod]
    public async Task Closing_the_owner_closes_the_owned_window_without_leaving_its_handle()
    {
        await ShellFixture.On(async shell =>
        {
            var owner = One();
            using var other = One();
            using var child = new FloatingWindow();
            child.Title = "Winshell test";
            child.Width = 320;
            child.Height = 200;
            child.Owner = owner;
            shell.Add(owner);
            shell.Add(other);
            shell.Add(child);
            var host = child.Host!;
            var hwnd = host.Handle;
            var otherHwnd = other.Host!.Handle;
            var closed = false;
            child.Closed += () => closed = true;

            owner.Dispose();

            Assert.IsFalse(PInvoke.IsWindow((HWND)hwnd));
            Assert.AreEqual(0, host.Handle);
            Assert.IsNull(child.Owner);
            Assert.IsTrue(PInvoke.IsWindow((HWND)otherHwnd));

            await Wait.UntilAsync(() => closed, "the owned window to say it closed");
            Assert.IsFalse(child.Visible);
            Assert.IsNull(child.Host);

            child.Visible = true;
            var again = child.Host!.Handle;
            Assert.AreNotEqual(hwnd, again);
            Assert.IsTrue(PInvoke.IsWindow((HWND)again));
            Assert.AreEqual(0, PInvoke.GetWindowLongPtr((HWND)again, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT));
            return true;
        });
    }


    private static RECT Restored(nint hwnd)
    {
        var place = new WINDOWPLACEMENT { length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>() };
        Assert.IsTrue(PInvoke.GetWindowPlacement((HWND)hwnd, ref place));
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
        Assert.IsNotNull(monitor);
        var bounds = monitor.Bounds;
        var rect = Outer(hwnd);
        Assert.IsInRange(bounds.left - 1, bounds.left + 1, rect.left);
        Assert.IsInRange(bounds.top - 1, bounds.top + 1, rect.top);
        Assert.IsInRange(bounds.right - 1, bounds.right + 1, rect.right);
        Assert.IsInRange(bounds.bottom - 1, bounds.bottom + 1, rect.bottom);
        Assert.AreEqual(0u, Style(hwnd) & (uint)(WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_THICKFRAME));
        if ((monitor.Work.left, monitor.Work.top, monitor.Work.right, monitor.Work.bottom) !=
            (monitor.Bounds.left, monitor.Bounds.top, monitor.Bounds.right, monitor.Bounds.bottom))
            Assert.IsTrue(
                rect.left < monitor.Work.left || rect.top < monitor.Work.top
                                              || rect.right > monitor.Work.right || rect.bottom > monitor.Work.bottom);
    }

    private static void AssertFramed(nint hwnd)
    {
        var style = Style(hwnd);
        Assert.AreNotEqual(0u, style & (uint)WINDOW_STYLE.WS_CAPTION);
        Assert.AreNotEqual(0u, style & (uint)WINDOW_STYLE.WS_THICKFRAME);
    }

    private static uint Style(nint hwnd)
    {
        return (uint)PInvoke.GetWindowLongPtr((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
    }

    private static RECT Outer(nint hwnd)
    {
        Assert.IsTrue(PInvoke.GetWindowRect((HWND)hwnd, out var rect));
        return rect;
    }
}
