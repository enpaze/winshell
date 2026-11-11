using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal static unsafe class WindowTree
{
    /// <summary>The desktop window, which top-level z-order changes name.</summary>
    internal static nint Desktop => PInvoke.GetDesktopWindow();

    /// <summary>
    /// Every window below <paramref name="parent"/>, its children's children too, each once, from a list Windows copies
    /// at once.
    /// </summary>
    internal static HashSet<nint> Descendants(nint parent)
    {
        return [.. Callback.Collect(context => PInvoke.EnumChildWindows((HWND)parent, &Collect, context))];
    }

    /// <summary>
    /// Every message-only window on this desktop, each once, from a list Windows copies at once. A walk from one window
    /// to the next ends early, with no error, where a window closes, and the newest windows, where it starts, come and
    /// go the most.
    /// </summary>
    internal static HashSet<nint> MessageOnly()
    {
        // Only a message-only window names their parent. Another app's may close before it is asked; this one can't.
        var own = PInvoke.CreateWindowEx(default, "STATIC", "", default, 0, 0, 0, 0, HWND.HWND_MESSAGE, default,
            default, null);
        if ((nint)own == 0)
            return [];
        var parent = (nint)PInvoke.GetAncestor(own, GET_ANCESTOR_FLAGS.GA_PARENT);
        PInvoke.DestroyWindow(own);
        var windows = parent != 0 ? Descendants(parent) : [];
        // The list also has the child windows of message-only windows.
        windows.RemoveWhere(hwnd => (nint)PInvoke.GetAncestor((HWND)hwnd, GET_ANCESTOR_FLAGS.GA_PARENT) != parent);
        return windows;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL Collect(HWND hwnd, LPARAM context)
    {
        return (BOOL)Callback.Collected(context, hwnd);
    }
}
