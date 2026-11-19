using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Apps;
using Winshell.Native;

namespace Winshell.AppWindows;

internal static class WindowRules
{
    public static string AppId(string windowProperty, string packageAppId, string executable)
    {
        if (windowProperty.Length > 0)
            return windowProperty;
        if (packageAppId.Length > 0)
            return packageAppId;
        return AppIdentity.DesktopId(executable);
    }

    public static bool IsWindowObject(int idObject, int idChild)
    {
        return idObject == (int)OBJECT_IDENTIFIER.OBJID_WINDOW && idChild == (int)PInvoke.CHILDID_SELF;
    }

    public static bool MarksWindow(uint eventType, nint hwnd, int idObject, int idChild, bool tracked, bool isRoot)
    {
        if (eventType == PInvoke.EVENT_SYSTEM_FOREGROUND)
            return hwnd != 0;
        if (hwnd == 0 || !IsWindowObject(idObject, idChild))
            return false;
        return eventType == PInvoke.EVENT_OBJECT_DESTROY ? tracked : isRoot;
    }

    public static bool CoversMonitor(long style, RECT window, RECT monitor)
    {
        return (style & (uint)WINDOW_STYLE.WS_CAPTION) != (uint)WINDOW_STYLE.WS_CAPTION
               && window.left <= monitor.left && window.top <= monitor.top && window.right >= monitor.right &&
               window.bottom >= monitor.bottom;
    }

    public static bool MarksForeground(uint eventType)
    {
        return eventType is PInvoke.EVENT_SYSTEM_FOREGROUND
            or PInvoke.EVENT_SYSTEM_MINIMIZESTART
            or PInvoke.EVENT_SYSTEM_MINIMIZEEND
            or PInvoke.EVENT_OBJECT_DESTROY
            or PInvoke.EVENT_OBJECT_HIDE;
    }

    public static bool IsFlash(nint code)
    {
        return code == ShellHookWindow.Flash;
    }

    public static void SettleAttention(HashSet<nint> wanting, nint active, Func<nint, bool> listed)
    {
        wanting.RemoveWhere(hwnd => hwnd == active || !listed(hwnd));
    }
}
