namespace Winshell.Bluetooth;

/// <summary>The state of a <see cref="BluetoothAdapter"/>'s radio.</summary>
public enum BluetoothAdapterState
{
    /// <summary>Windows does not say, or the radio could not be read.</summary>
    [PublicAPI] Unknown,

    /// <summary>The radio is off, and can be turned on.</summary>
    [PublicAPI] Off,

    /// <summary>The radio is on.</summary>
    [PublicAPI] On,

    /// <summary>The radio is off, and a hardware switch or the device's firmware keeps it off.</summary>
    [PublicAPI] Disabled
}

/// <summary>Whether a <see cref="BluetoothDevice"/> is connected.</summary>
public enum BluetoothDeviceState
{
    /// <summary>The device is not connected.</summary>
    [PublicAPI] Disconnected,

    /// <summary>The device is connected.</summary>
    [PublicAPI] Connected
}

/// <summary>The action running on a <see cref="BluetoothDevice"/>.</summary>
public enum BluetoothOperation
{
    /// <summary>No action is running.</summary>
    [PublicAPI] None,

    /// <summary><see cref="BluetoothDevice.Connect"/> is running.</summary>
    [PublicAPI] Connecting,

    /// <summary><see cref="BluetoothDevice.Disconnect"/> is running.</summary>
    [PublicAPI] Disconnecting,

    /// <summary><see cref="BluetoothDevice.Pair"/> is running.</summary>
    [PublicAPI] Pairing,

    /// <summary><see cref="BluetoothDevice.Unpair"/> is running.</summary>
    [PublicAPI] Unpairing
}
