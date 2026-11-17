using Winshell.Native;

namespace Winshell.ExplorerTaskbar;

internal static class ExplorerTaskbarRecovery
{
    internal static bool AnyHidden()
    {
        return ExplorerTaskbarWindow.Find().Windows.Any(taskbar => !PInvoke.IsWindowVisible((HWND)taskbar.Hwnd));
    }

    internal static void RestoreHidden()
    {
        var (windows, autohide) = ExplorerTaskbarWindow.Find();
        foreach (var (hwnd, primary) in windows)
        {
            if (PInvoke.IsWindowVisible((HWND)hwnd))
                continue;
            var saved = ExplorerTaskbarWindow.Saved.Of(hwnd, primary, autohide, true);
            saved.Removed = saved.Reserves;
            ExplorerTaskbarWindow.PutBack(saved);
        }
    }
}
