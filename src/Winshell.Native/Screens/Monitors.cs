using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Winshell.Native;

/// <summary>A monitor's device name, such as <c>\\.\DISPLAY1</c>, and its rectangles in physical pixels.</summary>
internal readonly record struct MonitorInfo(string Device, RECT Bounds, RECT Work, bool IsPrimary);

/// <summary>Reads the connected monitors from Windows.</summary>
internal static unsafe class Monitors
{
    /// <summary>The handle of every connected monitor, in the order Windows lists them.</summary>
    internal static List<nint> List()
    {
        return Callback.Collect(context => PInvoke.EnumDisplayMonitors(default, (RECT*)null, &Collect, context));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL Collect(HMONITOR monitor, HDC hdc, RECT* rect, LPARAM context)
    {
        return (BOOL)Callback.Collected(context, monitor);
    }

    /// <summary>The monitor's device name and rectangles. False once the monitor is gone.</summary>
    internal static bool TryRead(nint monitor, out MonitorInfo info)
    {
        var mi = new MONITORINFOEXW { monitorInfo = { cbSize = (uint)sizeof(MONITORINFOEXW) } };
        if (!PInvoke.GetMonitorInfo((HMONITOR)monitor, (MONITORINFO*)&mi))
        {
            info = default;
            return false;
        }

        info = new MonitorInfo(mi.szDevice.ToString(), mi.monitorInfo.rcMonitor, mi.monitorInfo.rcWork,
            (mi.monitorInfo.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0);
        return true;
    }

    /// <summary>The display device's current refresh rate in hertz, or null when Windows does not say.</summary>
    internal static uint? RefreshRate(string device)
    {
        var mode = new DEVMODEW { dmSize = (ushort)sizeof(DEVMODEW) };
        return PInvoke.EnumDisplaySettings(device, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref mode)
            ? mode.dmDisplayFrequency
            : null;
    }

    /// <summary>A fixed character buffer up to its first null, never past its capacity.</summary>
    internal static string Text(char* buffer, int capacity)
    {
        var text = new ReadOnlySpan<char>(buffer, capacity);
        var end = text.IndexOf('\0');
        return new string(end < 0 ? text : text[..end]);
    }
}
