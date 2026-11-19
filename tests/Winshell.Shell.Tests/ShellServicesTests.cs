using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Input;
using Winshell.Native;

namespace Winshell.ShellTests;

[TestClass]
[TestCategory("Window")]
public sealed class ShellServicesTests
{
    [TestMethod]
    public async Task A_hotkey_claims_its_sequence_runs_its_action_and_gives_the_chord_back()
    {
        var (active, presses, freed) = await ShellFixture.On(shell =>
        {
            var presses = 0;
            var hotkey = shell.Hotkey("Ctrl+Alt+Shift+F24", () => presses++);
            var active = hotkey.Active;
            ((IHotkeyListener)hotkey).OnPressed();
            hotkey.Dispose();
            const HOT_KEY_MODIFIERS modifiers = HOT_KEY_MODIFIERS.MOD_CONTROL | HOT_KEY_MODIFIERS.MOD_ALT |
                HOT_KEY_MODIFIERS.MOD_SHIFT;
            bool freed = PInvoke.RegisterHotKey(default, 1, modifiers, 0x87);
            if (freed)
                PInvoke.UnregisterHotKey(default, 1);
            return (active, presses, freed);
        });

        Assert.IsTrue(active);
        Assert.AreEqual(1, presses);
        Assert.IsTrue(freed);
    }

    [TestMethod]
    public async Task Winland_resolves_windows_from_the_shells_window_list()
    {
        var resolved = await ShellFixture.On(async shell =>
        {
            var winland = shell.Winland;
            var hwnd = NativeWindow.OfClass("STATIC", "winshell-listed-window",
                (uint)WINDOW_STYLE.WS_OVERLAPPEDWINDOW, 0, 80, 80, 40, 0, PInvoke.GetModuleHandle(null));
            Assert.AreNotEqual(0, hwnd);
            try
            {
                PInvoke.ShowWindow((HWND)hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
                await Wait.UntilAsync(() => shell.AppWindows.Find(hwnd) is not null,
                    "the app windows to list the window");
                return ReferenceEquals(shell.AppWindows.Find(hwnd), winland.FindWindow(hwnd));
            }
            finally
            {
                PInvoke.DestroyWindow((HWND)hwnd);
            }
        });

        Assert.IsTrue(resolved);
    }

    [TestMethod]
    public async Task Winland_resolves_windows_without_the_shell_reading_its_window_list()
    {
        var resolved = await ShellFixture.On(async shell =>
        {
            var winland = shell.Winland;
            var hwnd = NativeWindow.OfClass("STATIC", "winshell-winland-only-window",
                (uint)WINDOW_STYLE.WS_OVERLAPPEDWINDOW, 0, 80, 80, 40, 0, PInvoke.GetModuleHandle(null));
            Assert.AreNotEqual(0, hwnd);
            try
            {
                PInvoke.ShowWindow((HWND)hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
                await Wait.UntilAsync(() => winland.FindWindow(hwnd) is not null,
                    "Winland to resolve the window");
                return winland.FindWindow(hwnd)!.Title;
            }
            finally
            {
                PInvoke.DestroyWindow((HWND)hwnd);
            }
        });

        Assert.AreEqual("winshell-winland-only-window", resolved);
    }
}
