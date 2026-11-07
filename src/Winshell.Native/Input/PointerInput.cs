using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winshell.Native;

/// <summary>
/// Moves and presses the real mouse with <c>SendInput</c>, so WinUI raises its pointer events. Each call is one
/// injection that nothing else can split, and it acts where the cursor is: the caller checks what is under it first.
/// Its wheel does not reach a WinUI window, which takes the one <see cref="InputSite.PostWheel"/> posts.
/// </summary>
internal static unsafe class PointerInput
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    /// <summary>The cursor's place in physical pixels, or null when Windows won't say, as on a secure desktop.</summary>
    internal static POINT? Cursor()
    {
        return Win32.GetCursorPos(out var point) ? point : null;
    }

    /// <summary>The top-level window under a point on the screen, or 0.</summary>
    internal static nint RootWindowAt(POINT point)
    {
        var hwnd = Win32.WindowFromPoint(point);
        return hwnd == 0 ? 0 : TopLevelWindows.RootOf(hwnd);
    }

    /// <summary>Moves the mouse to a point on the screen, as a mouse does, so the window there sees it come.</summary>
    internal static bool MoveTo(POINT point)
    {
        // Absolute moves span the virtual desktop from 0 to 65535.
        var left = Win32.GetSystemMetrics(SmXVirtualScreen);
        var top = Win32.GetSystemMetrics(SmYVirtualScreen);
        var width = Math.Max(2, Win32.GetSystemMetrics(SmCxVirtualScreen));
        var height = Math.Max(2, Win32.GetSystemMetrics(SmCyVirtualScreen));
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi.dx = (int)Math.Round((point.X - left) * 65535.0 / (width - 1));
        input.Anonymous.mi.dy = (int)Math.Round((point.Y - top) * 65535.0 / (height - 1));
        input.Anonymous.mi.dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE |
                                     MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;
        return Send([input]);
    }

    /// <summary>A button's press and release where the cursor is, together, so the button is never left down.</summary>
    internal static bool Press(bool right)
    {
        var inputs = new INPUT[2];
        inputs[0].type = inputs[1].type = INPUT_TYPE.INPUT_MOUSE;
        inputs[0].Anonymous.mi.dwFlags =
            right ? MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN : MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN;
        inputs[1].Anonymous.mi.dwFlags =
            right ? MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP : MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP;
        return Send(inputs);
    }

    private static bool Send(INPUT[] inputs)
    {
        SentInput.Mark();
        fixed (INPUT* first = inputs)
        {
            return PInvoke.SendInput((uint)inputs.Length, first, sizeof(INPUT)) == inputs.Length;
        }
    }
}
