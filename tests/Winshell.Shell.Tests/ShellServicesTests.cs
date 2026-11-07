using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell.ShellTests;

[Trait("Category", "Slow")]
public sealed class ShellServicesTests
{
    [Fact]
    public async Task Winland_resolves_windows_from_the_shells_window_list()
    {
        var resolved = await ShellFixture.On(async shell =>
        {
            var winland = shell.Winland;
            var hwnd = Win32.CreateWindowExW(0, "STATIC", "winshell-listed-window",
                (uint)WINDOW_STYLE.WS_OVERLAPPEDWINDOW, 0, 80, 80, 40, 0, 0, Win32.GetModuleHandleW(null), 0);
            Assert.NotEqual(0, hwnd);
            try
            {
                Win32.ShowWindow(hwnd, (int)SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
                await Wait.UntilAsync(() => shell.WindowList.Find(hwnd) is not null,
                    "the window list to list the window");
                return ReferenceEquals(shell.WindowList.Find(hwnd), winland.FindWindow(hwnd));
            }
            finally
            {
                Win32.DestroyWindow(hwnd);
            }
        });

        Assert.True(resolved);
    }

    [Fact]
    public async Task A_hotkey_with_no_sequence_takes_no_keys()
    {
        var registered = await ShellFixture.On(shell =>
        {
            using var hotkey = shell.NewHotkey();
            return hotkey.Active;
        });

        Assert.False(registered);
    }
}
