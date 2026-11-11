using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell.ShellTests;

[Trait("Category", "Window")]
public sealed class PanelMenuTests
{
    /// <summary>
    /// A tray app that opens its menu without the foreground has a menu a click elsewhere never closes. The thread
    /// here is that app: a press on a panel still has to close its menu, and the panel takes no activation.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_panel_press_closes_a_menu_opened_without_the_foreground_and_takes_no_activation(
        bool mouseActivation)
    {
        // Another test's press closes every menu too, so none runs until this menu is gone.
        await ShellFixture.Hold(async () =>
        {
            var menu = TrayAppMenu.Open();

            await ShellFixture.On(shell =>
            {
                using var panel = new PanelWindow();
                panel.Content = new Grid();
                using var host = new HostWindow(shell.Platform, WindowKind.Panel, panel);
                var foreground = PInvoke.GetForegroundWindow();
                Assert.True(menu.Shows);

                if (mouseActivation)
                    Assert.Equal((nint)PInvoke.MA_NOACTIVATE,
                        (nint)PInvoke.SendMessage((HWND)host.Handle, PInvoke.WM_MOUSEACTIVATE, 0, 0));
                else
                    // What `winshell inspect click` does before it invokes a control.
                    host.Messages.Press();

                Assert.Equal(foreground, PInvoke.GetForegroundWindow());
                return true;
            });

            await menu.Closed();
        });
    }

    /// <summary>A popup menu shown on a thread of its own that never has the foreground.</summary>
    private sealed class TrayAppMenu
    {
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile uint _thread;
        private TimeSpan _opened;
        private string _before = "";

        public bool Shows => MenuShowsOn(_thread);

        public static TrayAppMenu Open()
        {
            var menu = new TrayAppMenu();
            var app = new Thread(menu.Show) { IsBackground = true, Name = "Tray app" };
            var started = TimeProvider.System.GetTimestamp();
            app.SetApartmentState(ApartmentState.STA);
            app.Start();
            Wait.Until(() => menu._thread != 0 && MenuShowsOn(menu._thread), "the app's menu to open");
            menu._opened = TimeProvider.System.GetElapsedTime(started);
            menu._before = State(menu._thread);
            return menu;
        }

        /// <summary>Waits for the menu to close. This failed once on CI and never here, so a menu that stays open says what its thread was doing.</summary>
        public async Task Closed()
        {
            try
            {
                await _closed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            }
            catch (TimeoutException)
            {
                Assert.Fail(
                    $"The menu stayed open. It took {_opened.TotalMilliseconds:F0} ms to open. Its thread before the press: {_before}. After: {State(_thread)}.");
            }
        }

        private void Show()
        {
            _thread = Win32.GetCurrentThreadId();
            var owner = NativeWindow.OfClass("STATIC", "", 0, 0, 0, 0, 0, 0, 0);
            var menu = PInvoke.CreatePopupMenu();
            try
            {
                _ = PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, 1, "Item");
                _ = PInvoke.TrackPopupMenuEx(menu,
                    (uint)(TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_NONOTIFY), 0, 0,
                    (HWND)owner);
                _closed.SetResult();
            }
            finally
            {
                _ = PInvoke.DestroyMenu(menu);
                _ = PInvoke.DestroyWindow((HWND)owner);
            }
        }

        /// <summary>What Windows says about a thread's menu: whether one shows, and the thread's menu mode, owner and capture.</summary>
        private static string State(uint thread)
        {
            var info = new GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() };
            bool known = PInvoke.GetGUIThreadInfo(thread, ref info);
            return
                $"menu shows {MenuShowsOn(thread)}, info read {known}, flags 0x{(uint)info.flags:x}, owner {info.hwndMenuOwner != default}, capture {info.hwndCapture != default}";
        }

        private static bool MenuShowsOn(uint thread)
        {
            nint menu = 0;
            while ((menu = PInvoke.FindWindowEx(HWND.Null, (HWND)menu, "#32768", null)) != 0)
                if (PInvoke.IsWindowVisible((HWND)menu) && PInvoke.GetWindowThreadProcessId((HWND)menu, out _) == thread)
                    return true;
            return false;
        }
    }
}
