using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace Winshell.Native;

internal static class DisplayDevice
{
    internal static unsafe (string Model, string Id) Monitor(string deviceName)
    {
        var info = new DISPLAY_DEVICEW { cb = (uint)sizeof(DISPLAY_DEVICEW) };
        if (!PInvoke.EnumDisplayDevices(deviceName, 0, ref info, PInvoke.EDD_GET_DEVICE_INTERFACE_NAME))
            return ("", "");
        return (info.DeviceString.ToString(), info.DeviceID.ToString());
    }
}
