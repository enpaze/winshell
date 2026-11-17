using System.Runtime.InteropServices;
using Winshell.Native;

namespace Winshell.Windows;

internal static class WindowDesktop
{
    private static WindowDesktopManager? _manager;

    public static string IdOf(nint hwnd)
    {
        if (hwnd == 0)
            return "";
        try
        {
            var manager = Manager(out _);
            if (manager is null)
                return "";
            if (manager.DesktopOf(hwnd, out var id) < 0 || id == Guid.Empty)
                return "";
            return id.ToString("D");
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidComObjectException)
        {
            _manager = null;
            return "";
        }
    }

    public static bool Move(nint hwnd, Guid desktop, out string problem)
    {
        if (hwnd == 0 || desktop == Guid.Empty)
        {
            problem = "MoveWindowToDesktop needs a window and a desktop.";
            return false;
        }

        try
        {
            var manager = Manager(out problem);
            if (manager is null)
                return false;
            var hr = manager.Move(hwnd, desktop);
            if (hr < 0)
            {
                problem = $"MoveWindowToDesktop failed (0x{hr:X8}).";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidComObjectException)
        {
            _manager = null;
            problem = $"MoveWindowToDesktop failed ({ex.GetType().Name}).";
            return false;
        }
    }

    private static WindowDesktopManager? Manager(out string problem)
    {
        problem = "";
        if (_manager is not null)
            return _manager;
        _manager = WindowDesktopManager.Create(out var hr);
        if (_manager is null)
            problem = $"IVirtualDesktopManager is unavailable (0x{hr:X8}).";
        return _manager;
    }
}
