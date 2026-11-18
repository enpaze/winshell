using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal static unsafe class WindowTree
{
    internal static nint Desktop => PInvoke.GetDesktopWindow();

    internal static HashSet<nint> Descendants(nint parent)
    {
        return [.. Callback.Collect(context => PInvoke.EnumChildWindows((HWND)parent, &Callback.CollectWindow, context))];
    }

    internal static HashSet<nint> MessageOnly()
    {
        var own = PInvoke.CreateWindowEx(default, "STATIC", "", default, 0, 0, 0, 0, HWND.HWND_MESSAGE, default,
            default, null);
        if ((nint)own == 0)
            return [];
        var parent = (nint)PInvoke.GetAncestor(own, GET_ANCESTOR_FLAGS.GA_PARENT);
        PInvoke.DestroyWindow(own);
        var windows = parent != 0 ? Descendants(parent) : [];
        windows.RemoveWhere(hwnd => (nint)PInvoke.GetAncestor((HWND)hwnd, GET_ANCESTOR_FLAGS.GA_PARENT) != parent);
        return windows;
    }
}
