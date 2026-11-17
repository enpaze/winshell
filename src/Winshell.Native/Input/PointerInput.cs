using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal static class PointerInput
{
    internal static POINT? Cursor()
    {
        if (!PInvoke.GetCursorPos(out System.Drawing.Point point))
            return null;
        return new POINT { X = point.X, Y = point.Y };
    }

    internal static nint RootWindowAt(POINT point)
    {
        var hwnd = (nint)PInvoke.WindowFromPoint(new System.Drawing.Point(point.X, point.Y));
        return hwnd == 0 ? 0 : TopLevelWindows.RootOf(hwnd);
    }

    internal static bool MoveTo(POINT point, bool asMouse = true)
    {
        if (!asMouse)
            return SentInput.Around(() => PInvoke.SetCursorPos(point.X, point.Y));
        return SentInput.Send([Move(point)], 1);
    }

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
