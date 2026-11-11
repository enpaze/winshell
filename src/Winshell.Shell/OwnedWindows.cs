using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell;

/// <summary>Keeps an owned window just above its owner in the z-order, and leaves every other window where it is.</summary>
internal static class OwnedWindows
{
    /// <summary>
    /// Makes <paramref name="owner"/> own <paramref name="window"/>, or nothing when it is gone. The chain has no
    /// loop: <see cref="FloatingWindow.Owner"/> refuses one.
    /// </summary>
    public static void Link(nint window, HostWindow? owner)
    {
        var hwnd = owner is { Handle: not 0 } && PInvoke.IsWindow((HWND)owner.Handle) ? owner.Handle : 0;
        if (PInvoke.GetWindowLongPtr((HWND)window, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT) == hwnd)
            return;
        PInvoke.SetWindowLongPtr((HWND)window, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT, hwnd);
        if (hwnd != 0)
            PlaceAboveOwner(window);
    }

    /// <summary>Puts the windows <paramref name="owner"/> owns back above it, as after showing or restoring it.</summary>
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
        // InsertAfter is the window above this one. Nothing above the owner means the top of the z-order.
        PInvoke.SetWindowPos((HWND)window, above == 0 ? HWND.HWND_TOP : above, 0, 0, 0, 0,
            (SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                   SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER));
    }
}
