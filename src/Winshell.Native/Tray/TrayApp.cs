using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static class TrayApp
{
    internal static void Notify(nint window, uint message, nint wParam, nint lParam)
    {
        _ = PInvoke.SendNotifyMessage((HWND)window, message, (nuint)wParam, lParam);
    }

    internal static void AskForIcons()
    {
        PInvoke.PostMessage(HWND.HWND_BROADCAST, TaskbarWindows.Created, default, default);
        foreach (var window in WindowTree.MessageOnly())
            PInvoke.PostMessage((HWND)window, TaskbarWindows.Created, default, default);
    }

    internal static bool AllowForeground(uint processId)
    {
        return PInvoke.AllowSetForegroundWindow(processId);
    }
}
