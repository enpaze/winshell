using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell;

internal static class OwnedWindows
{
    public static void Link(nint window, HostWindow? owner)
    {
        var hwnd = owner is { Handle: not 0 } && PInvoke.IsWindow((HWND)owner.Handle) ? owner.Handle : 0;
        if (PInvoke.GetWindowLongPtr((HWND)window, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT) == hwnd)
            return;
        PInvoke.SetWindowLongPtr((HWND)window, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT, hwnd);
        if (hwnd != 0)
            PlaceAboveOwner(window);
    }

    public static void Raise(nint owner, IEnumerable<HostWindow> windows)
    {
        foreach (var window in windows)
            if (window.Handle != 0 && PInvoke.GetWindowLongPtr((HWND)window.Handle, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT) == owner)
                PlaceAboveOwner(window.Handle);
    }

    private static void PlaceAboveOwner(nint window)
    {
        var owner = PInvoke.GetWindowLongPtr((HWND)window, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT);
        if (owner == 0 || !PInvoke.IsWindow((HWND)owner))
            return;
        var above = PInvoke.GetWindow((HWND)owner, GET_WINDOW_CMD.GW_HWNDPREV);
        if (above == window)
            return;
        PInvoke.SetWindowPos((HWND)window, above == 0 ? HWND.HWND_TOP : above, 0, 0, 0, 0,
            (SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                   SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER));
    }
}
