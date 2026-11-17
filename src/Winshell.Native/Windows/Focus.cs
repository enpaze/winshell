using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static class Focus
{
    internal static void Take(nint window)
    {
        if (window == 0 || (nint)PInvoke.GetForegroundWindow() == window)
            return;
        if (Ask(window))
            return;

        var foreground = (nint)PInvoke.GetForegroundWindow();
        var current = PInvoke.GetCurrentThreadId();
        var other = foreground == 0 ? 0 : PInvoke.GetWindowThreadProcessId((HWND)foreground);
        if (other != 0 && other != current && PInvoke.AttachThreadInput(current, other, true))
            try
            {
                TopLevelWindows.Raise(window);
                Ask(window);
            }
            finally
            {
                PInvoke.AttachThreadInput(current, other, false);
            }

        if ((nint)PInvoke.GetForegroundWindow() == window)
            return;
        KeyInput.Tap();
        Ask(window);
    }

    private static bool Ask(nint window)
    {
        return PInvoke.SetForegroundWindow((HWND)window) && (nint)PInvoke.GetForegroundWindow() == window;
    }
}
