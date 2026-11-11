using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Winshell.Native;

/// <summary>Which edge of a screen an app bar reserves.</summary>
internal enum AppBarEdge
{
    Left,
    Top,
    Right,
    Bottom
}

/// <summary>
/// Explorer's app-bar protocol for one window. It builds <c>APPBARDATA</c> and sends New, QueryPos, SetPos and Remove,
/// and it reads whether the taskbar auto-hides and where that taskbar sits. Callers choose the rectangle they ask for
/// and what a grant means: a panel's work area and a taskbar's hide-and-restore stay with them. A caller that already
/// holds the rectangle it would ask for skips <see cref="Reserve"/>; asking again would move the work area forever.
/// </summary>
internal static class AppBars
{
    /// <summary>
    /// Registers <paramref name="window"/> so Explorer posts <paramref name="callback"/> to it. False when no host
    /// answers, as when Explorer is not running.
    /// </summary>
    internal static bool Register(nint window, uint callback)
    {
        var data = Data(window);
        data.uCallbackMessage = callback;
        return PInvoke.SHAppBarMessage(PInvoke.ABM_NEW, ref data) != 0;
    }

    /// <summary>The part of <paramref name="edge"/> still free inside <paramref name="proposed"/>.</summary>
    internal static RECT Query(nint window, AppBarEdge edge, RECT proposed)
    {
        var data = Data(window);
        data.uEdge = Edge(edge);
        data.rc = proposed;
        PInvoke.SHAppBarMessage(PInvoke.ABM_QUERYPOS, ref data);
        return data.rc;
    }

    /// <summary>Reserves <paramref name="desired"/> on <paramref name="edge"/> and returns the rectangle Explorer granted.</summary>
    internal static RECT Reserve(nint window, AppBarEdge edge, RECT desired)
    {
        var data = Data(window);
        data.uEdge = Edge(edge);
        data.rc = desired;
        PInvoke.SHAppBarMessage(PInvoke.ABM_SETPOS, ref data);
        return data.rc;
    }

    /// <summary>Removes <paramref name="window"/>'s app bar. False when no host answers.</summary>
    internal static bool Remove(nint window)
    {
        var data = Data(window);
        return PInvoke.SHAppBarMessage(PInvoke.ABM_REMOVE, ref data) != 0;
    }

    /// <summary>Whether the taskbar auto-hides. An auto-hidden taskbar reserves no edge.</summary>
    internal static bool Autohide(nint window)
    {
        var data = Data(window);
        var state = (uint)PInvoke.SHAppBarMessage(PInvoke.ABM_GETSTATE, ref data);
        return (state & PInvoke.ABS_AUTOHIDE) != 0;
    }

    /// <summary>The taskbar's edge and rectangle, when Explorer reports one that has area.</summary>
    internal static bool TryPosition(nint window, out AppBarEdge edge, out RECT bounds)
    {
        var data = Data(window);
        if (PInvoke.SHAppBarMessage(PInvoke.ABM_GETTASKBARPOS, ref data) != 0 &&
            data.rc is { Width: > 0, Height: > 0 })
        {
            edge = From(data.uEdge);
            bounds = data.rc;
            return true;
        }

        edge = default;
        bounds = default;
        return false;
    }

    /// <summary>Whether <paramref name="parameter"/> is Explorer's notice that app-bar positions changed.</summary>
    internal static bool PositionChanged(nint parameter)
    {
        return (uint)parameter == PInvoke.ABN_POSCHANGED;
    }

    private static APPBARDATA Data(nint window)
    {
        return new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>(), hWnd = (HWND)window };
    }

    private static uint Edge(AppBarEdge edge)
    {
        return edge switch
        {
            AppBarEdge.Left => PInvoke.ABE_LEFT,
            AppBarEdge.Right => PInvoke.ABE_RIGHT,
            AppBarEdge.Bottom => PInvoke.ABE_BOTTOM,
            _ => PInvoke.ABE_TOP
        };
    }

    private static AppBarEdge From(uint edge)
    {
        return edge switch
        {
            PInvoke.ABE_LEFT => AppBarEdge.Left,
            PInvoke.ABE_RIGHT => AppBarEdge.Right,
            PInvoke.ABE_BOTTOM => AppBarEdge.Bottom,
            _ => AppBarEdge.Top
        };
    }
}
