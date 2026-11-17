using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Winshell.Native;

internal static class MonitorBrightness
{
    private const uint MaxPhysicalMonitors = 16;

    internal static bool TryRead(nint monitor, out int minimum, out int current, out int maximum)
    {
        uint low = 0, now = 0, high = 0;
        var ok = WithFirstPhysical(monitor,
            physical => PInvoke.GetMonitorBrightness(physical, out low, out now, out high) != 0);
        minimum = (int)low;
        current = (int)now;
        maximum = (int)high;
        return ok && maximum >= minimum;
    }

    internal static bool TrySet(nint monitor, uint brightness)
    {
        return WithFirstPhysical(monitor, physical => PInvoke.SetMonitorBrightness(physical, brightness) != 0);
    }

    private static bool WithFirstPhysical(nint monitor, Func<HANDLE, bool> use)
    {
        var display = new HMONITOR(monitor);
        if (!PInvoke.GetNumberOfPhysicalMonitorsFromHMONITOR(display, out var count) || count == 0 ||
            count > MaxPhysicalMonitors)
            return false;
        Span<PHYSICAL_MONITOR> monitors = stackalloc PHYSICAL_MONITOR[(int)count];
        if (!PInvoke.GetPhysicalMonitorsFromHMONITOR(display, monitors))
            return false;
        try
        {
            return use(monitors[0].hPhysicalMonitor);
        }
        finally
        {
            PInvoke.DestroyPhysicalMonitors(monitors);
        }
    }
}
