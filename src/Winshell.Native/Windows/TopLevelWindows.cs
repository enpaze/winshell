using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

/// <summary>
/// Reads and asks things of top-level windows, including other programs'. Each request that changes a window is
/// posted without waiting, so a hung program cannot hold up the caller.
/// </summary>
internal static unsafe class TopLevelWindows
{
    /// <summary>The longest title read, in characters, so a program cannot make the caller allocate without bound.</summary>
    private const int MaxTitle = 4096;

    private const int ClassNameCapacity = 256;

    /// <summary>Every top-level window, in z-order from the top.</summary>
    internal static List<nint> List()
    {
        return Callback.Collect(context => PInvoke.EnumWindows(&Collect, context));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL Collect(HWND hwnd, LPARAM context)
    {
        return (BOOL)Callback.Collected(context, hwnd);
    }

    /// <summary>The window's title, cut at <see cref="MaxTitle"/> characters. Empty when it has none.</summary>
    internal static string Title(nint hwnd)
    {
        var length = Math.Min(PInvoke.GetWindowTextLength((HWND)hwnd) + 1, MaxTitle);
        if (length <= 1)
            return "";
        var buffer = stackalloc char[length];
        var got = PInvoke.GetWindowText((HWND)hwnd, buffer, length);
        return new string(buffer, 0, Math.Clamp(got, 0, length - 1));
    }

    internal static bool HasTitle(nint hwnd)
    {
        return PInvoke.GetWindowTextLength((HWND)hwnd) > 0;
    }

    /// <summary>The window's class name, or empty when it cannot be read.</summary>
    internal static string ClassName(nint hwnd)
    {
        var buffer = stackalloc char[ClassNameCapacity];
        var got = PInvoke.GetClassName((HWND)hwnd, buffer, ClassNameCapacity);
        return new string(buffer, 0, Math.Clamp(got, 0, ClassNameCapacity - 1));
    }

    /// <summary>
    /// Whether this is the desktop: the window Explorer draws the wallpaper and icons in, or a <c>WorkerW</c> window
    /// Explorer makes for it.
    /// </summary>
    internal static bool IsShellWindow(nint hwnd)
    {
        return hwnd == PInvoke.GetShellWindow() || ClassName(hwnd) is "Progman" or "WorkerW";
    }

    /// <summary>Whether DWM hides the window, as it does a window on another virtual desktop or a suspended app.</summary>
    internal static bool IsCloaked(nint hwnd)
    {
        int cloaked;
        return PInvoke.DwmGetWindowAttribute((HWND)hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int))
            .Succeeded && cloaked != 0;
    }

    internal static bool HasChildStyle(nint hwnd)
    {
        return (PInvoke.GetWindowLongPtr((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE) & (long)WINDOW_STYLE.WS_CHILD) != 0;
    }

    /// <summary>The top-level window that holds this one, or 0.</summary>
    internal static nint RootOf(nint hwnd)
    {
        return PInvoke.GetAncestor((HWND)hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);
    }

    /// <summary>The window's visible frame in screen pixels, without the invisible resize borders.</summary>
    internal static bool TryFrameBounds(nint hwnd, out RECT frame)
    {
        RECT bounds = default;
        var ok = PInvoke.DwmGetWindowAttribute((HWND)hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &bounds,
            (uint)sizeof(RECT)).Value == 0;
        frame = bounds;
        return ok;
    }

    /// <summary>Asks the window to close, as its close button does.</summary>
    internal static void Close(nint hwnd)
    {
        PInvoke.PostMessage((HWND)hwnd, PInvoke.WM_SYSCOMMAND, (nuint)PInvoke.SC_CLOSE, 0);
    }

    internal static void Restore(nint hwnd)
    {
        PInvoke.ShowWindowAsync((HWND)hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
    }

    internal static void Maximize(nint hwnd)
    {
        PInvoke.ShowWindowAsync((HWND)hwnd, SHOW_WINDOW_CMD.SW_MAXIMIZE);
    }

    internal static void Minimize(nint hwnd)
    {
        PInvoke.ShowWindowAsync((HWND)hwnd, SHOW_WINDOW_CMD.SW_MINIMIZE);
    }

    /// <summary>Puts the window at the top of the z-order without activating it.</summary>
    internal static void Raise(nint hwnd)
    {
        PInvoke.SetWindowPos((HWND)hwnd, HWND.HWND_TOP, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS);
    }

    /// <summary>Moves and sizes the window to <paramref name="rect"/>, in screen pixels, without activating it.</summary>
    internal static void Move(nint hwnd, RECT rect)
    {
        PInvoke.SetWindowPos((HWND)hwnd, HWND.HWND_TOP, rect.left, rect.top, rect.Width, rect.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS);
    }

    /// <summary>
    /// The window's placement, with its restored rectangle in workspace coordinates. False when Windows does not say.
    /// </summary>
    internal static bool TryPlacement(nint hwnd, out WINDOWPLACEMENT placement)
    {
        placement = new WINDOWPLACEMENT { length = (uint)sizeof(WINDOWPLACEMENT) };
        return PInvoke.GetWindowPlacement((HWND)hwnd, ref placement);
    }

    /// <summary>
    /// Gives the window the restored rectangle <paramref name="normal"/>, in workspace coordinates, and shows it
    /// minimized, maximized or restored. The rest of <paramref name="placement"/> stays as read.
    /// </summary>
    internal static void Place(nint hwnd, WINDOWPLACEMENT placement, RECT normal, bool minimized, bool maximized)
    {
        placement.rcNormalPosition = normal;
        placement.showCmd = minimized ? SHOW_WINDOW_CMD.SW_SHOWMINIMIZED :
            maximized ? SHOW_WINDOW_CMD.SW_MAXIMIZE : SHOW_WINDOW_CMD.SW_SHOWNORMAL;
        placement.flags |= WINDOWPLACEMENT_FLAGS.WPF_ASYNCWINDOWPLACEMENT;
        PInvoke.SetWindowPlacement((HWND)hwnd, in placement);
    }
}
