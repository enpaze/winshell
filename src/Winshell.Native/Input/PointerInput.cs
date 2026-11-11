using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

/// <summary>
/// Moves and presses the real mouse with <c>SendInput</c>, so WinUI raises its pointer events. Each call is one
/// injection that nothing else can split, at the point it is given: the caller checks what is there first.
/// Its wheel does not reach a WinUI window, which takes the one <see cref="InputSite.PostWheel"/> posts.
/// </summary>
internal static class PointerInput
{
    /// <summary>The cursor's place in physical pixels, or null when Windows won't say, as on a secure desktop.</summary>
    internal static POINT? Cursor()
    {
        if (!PInvoke.GetCursorPos(out System.Drawing.Point point))
            return null;
        return new POINT { X = point.X, Y = point.Y };
    }

    /// <summary>The top-level window under a point on the screen, or 0.</summary>
    internal static nint RootWindowAt(POINT point)
    {
        var hwnd = (nint)PInvoke.WindowFromPoint(new System.Drawing.Point(point.X, point.Y));
        return hwnd == 0 ? 0 : TopLevelWindows.RootOf(hwnd);
    }

    /// <summary>Moves the mouse to a point on the screen, as a mouse does, so the window there sees it come.</summary>
    internal static bool MoveTo(POINT point)
    {
        return SentInput.Send([Move(point)], 1);
    }

    /// <summary>
    /// A button's press and release at a point, with the move there, all in one injection: the press can't land
    /// anywhere the cursor went in between, and the button is never left down.
    /// </summary>
    internal static bool Press(POINT point, bool right)
    {
        var inputs = new INPUT[3];
        inputs[0] = Move(point);
        inputs[1].type = inputs[2].type = INPUT_TYPE.INPUT_MOUSE;
        inputs[1].Anonymous.mi.dwFlags =
            right ? MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN : MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN;
        inputs[2].Anonymous.mi.dwFlags =
            right ? MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP : MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP;
        return SentInput.Send(inputs, 2);
    }

    /// <summary>An absolute move, which spans the virtual desktop from 0 to 65535.</summary>
    private static INPUT Move(POINT point)
    {
        var left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
        var top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
        var width = Math.Max(2, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN));
        var height = Math.Max(2, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN));
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi.dx = (int)Math.Round((point.X - left) * 65535.0 / (width - 1));
        input.Anonymous.mi.dy = (int)Math.Round((point.Y - top) * 65535.0 / (height - 1));
        input.Anonymous.mi.dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE |
                                     MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;
        return input;
    }
}
