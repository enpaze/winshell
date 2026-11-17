using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static class InputSite
{
    private const string ClassName = "InputSiteWindowClass";
    private const int WheelDelta = 120;

    internal static nint Of(nint window)
    {
        return WindowTree.Descendants(window).FirstOrDefault(hwnd => TopLevelWindows.ClassName(hwnd) == ClassName);
    }

    internal static bool PostKey(nint site, int key)
    {
        var flags = 1 | ((nint)KeyInput.ScanCode(key) << 16) | (KeyInput.IsExtended(key) ? 1 << 24 : 0);
        return PInvoke.PostMessage((HWND)site, PInvoke.WM_KEYDOWN, (nuint)key, flags)
               && PInvoke.PostMessage((HWND)site, PInvoke.WM_KEYUP, (nuint)key, flags | unchecked((nint)0xC0000000));
    }

    internal static bool PostWheel(nint site, POINT at, bool down)
    {
        var delta = (ushort)(short)(down ? -WheelDelta : WheelDelta);
        return PInvoke.PostMessage((HWND)site, PInvoke.WM_MOUSEWHEEL, (nuint)((uint)delta << 16),
            ((at.Y & 0xFFFF) << 16) | (at.X & 0xFFFF));
    }
}
