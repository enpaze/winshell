using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static class BluetoothRadios
{
    internal static bool IsDiscoverable()
    {
        return PInvoke.BluetoothIsDiscoverable(HANDLE.Null);
    }

    internal static bool IsConnectable()
    {
        return PInvoke.BluetoothIsConnectable(HANDLE.Null);
    }

    internal static bool EnableDiscovery(bool enabled)
    {
        return PInvoke.BluetoothEnableDiscovery(HANDLE.Null, enabled);
    }

    internal static bool EnableIncomingConnections(bool enabled)
    {
        return PInvoke.BluetoothEnableIncomingConnections(HANDLE.Null, enabled);
    }
}
