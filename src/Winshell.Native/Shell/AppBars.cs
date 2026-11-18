using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Winshell.Native;

internal enum AppBarEdge
{
    Left,
    Top,
    Right,
    Bottom
}

internal static class AppBars
{
    internal static bool Register(nint window, uint callback)
    {
        var data = Data(window);
        data.uCallbackMessage = callback;
        return PInvoke.SHAppBarMessage(PInvoke.ABM_NEW, ref data) != 0;
    }

    internal static RECT Query(nint window, AppBarEdge edge, RECT proposed)
    {
        var data = Data(window);
        data.uEdge = Edge(edge);
        data.rc = proposed;
        PInvoke.SHAppBarMessage(PInvoke.ABM_QUERYPOS, ref data);
        return data.rc;
    }

    internal static RECT Reserve(nint window, AppBarEdge edge, RECT desired)
    {
        var data = Data(window);
        data.uEdge = Edge(edge);
        data.rc = desired;
        PInvoke.SHAppBarMessage(PInvoke.ABM_SETPOS, ref data);
        return data.rc;
    }

    internal static bool Remove(nint window)
    {
        var data = Data(window);
        return PInvoke.SHAppBarMessage(PInvoke.ABM_REMOVE, ref data) != 0;
    }

    internal static bool Autohide(nint window)
    {
        var data = Data(window);
        var state = (uint)PInvoke.SHAppBarMessage(PInvoke.ABM_GETSTATE, ref data);
        return (state & PInvoke.ABS_AUTOHIDE) != 0;
    }

    internal static uint State()
    {
        var data = Data(0);
        return (uint)PInvoke.SHAppBarMessage(PInvoke.ABM_GETSTATE, ref data);
    }

    internal static void SetState(uint state)
    {
        var data = Data(0);
        data.lParam = (LPARAM)(nint)state;
        PInvoke.SHAppBarMessage(PInvoke.ABM_SETSTATE, ref data);
    }

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
