namespace Winshell.Native;

/// <summary>
/// The hidden child of a WinUI window that takes its keyboard and wheel input. A message posted to it reaches that
/// window whether or not it is in front, moves no cursor, and reaches nothing else. A posted key carries no modifier:
/// Windows reads Shift and the rest from the real keyboard.
/// </summary>
internal static class InputSite
{
    private const string ClassName = "InputSiteWindowClass";
    private const int WheelDelta = 120;

    /// <summary>The input site of a top-level window, or 0 when it has none.</summary>
    internal static nint Of(nint window)
    {
        return WindowTree.Descendants(window).FirstOrDefault(hwnd => TopLevelWindows.ClassName(hwnd) == ClassName);
    }

    /// <summary>A key's press and release.</summary>
    internal static bool PostKey(nint site, int key)
    {
        // A repeat count of 1, the scan code and the extended flag; a release adds the previous state and transition bits.
        var flags = 1 | ((nint)KeyInput.ScanCode(key) << 16) | (KeyInput.IsExtended(key) ? 1 << 24 : 0);
        return Win32.PostMessageW(site, Win32.WM_KEYDOWN, key, flags)
               && Win32.PostMessageW(site, Win32.WM_KEYUP, key, flags | unchecked((nint)0xC0000000u));
    }

    /// <summary>
    /// Turns the wheel one notch at a point on the screen, toward the user, which scrolls down, or away. XAML gives it to
    /// the element at that point, wherever the cursor is.
    /// </summary>
    internal static bool PostWheel(nint site, POINT at, bool down)
    {
        var delta = (ushort)(short)(down ? -WheelDelta : WheelDelta);
        return Win32.PostMessageW(site, Win32.WM_MOUSEWHEEL, (nint)delta << 16,
            ((at.Y & 0xFFFF) << 16) | (at.X & 0xFFFF));
    }
}
