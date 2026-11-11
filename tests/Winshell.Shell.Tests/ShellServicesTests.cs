using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell.ShellTests;

[Trait("Category", "Window")]
public sealed class ShellServicesTests
{
    [Fact]
    public async Task Winland_resolves_windows_from_the_shells_window_list()
    {
        var resolved = await ShellFixture.On(async shell =>
        {
            var winland = shell.Winland;
            var hwnd = NativeWindow.OfClass("STATIC", "winshell-listed-window",
                (uint)WINDOW_STYLE.WS_OVERLAPPEDWINDOW, 0, 80, 80, 40, 0, Win32.GetModuleHandleW(null));
            Assert.NotEqual(0, hwnd);
            try
            {
                PInvoke.ShowWindow((HWND)hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
                await Wait.UntilAsync(() => shell.WindowList.Find(hwnd) is not null,
                    "the window list to list the window");
                return ReferenceEquals(shell.WindowList.Find(hwnd), winland.FindWindow(hwnd));
            }
            finally
            {
                PInvoke.DestroyWindow((HWND)hwnd);
            }
        });

        Assert.True(resolved);
    }
}
