# Winshell.Bluetooth

The Bluetooth adapter, paired and nearby devices, pairing and connecting. Reach them from `Shell.Current.Bluetooth`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## BluetoothAdapter

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Address` | string, read-only |
| `Discoverable` | bool |
| `Enabled` | bool |
| `Error` | string, read-only |
| `Id` | string, read-only |
| `Name` | string, read-only |
| `Pairable` | bool |
| `State` | [BluetoothAdapterState](#bluetoothadapterstate), read-only |
| `OperationFailed` | Action of string |

## BluetoothAdapterState

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `Off` | 1 |
| `On` | 2 |
| `Disabled` | 3 |

## BluetoothDevice

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Adapter` | [BluetoothAdapter](#bluetoothadapter), read-only |
| `Address` | string, read-only |
| `Battery` | double, read-only |
| `BatteryAvailable` | bool, read-only |
| `Busy` | bool, read-only |
| `CanPair` | bool, read-only |
| `Connected` | bool, read-only |
| `Error` | string, read-only |
| `Icon` | string, read-only |
| `Id` | string, read-only |
| `Name` | string, read-only |
| `Operation` | [BluetoothOperation](#bluetoothoperation), read-only |
| `Paired` | bool, read-only |
| `Pairing` | bool, read-only |
| `Present` | bool, read-only |
| `State` | [BluetoothDeviceState](#bluetoothdevicestate), read-only |
| `OperationFailed` | Action of string |
| `CancelPairing()` | void |
| `Connect()` | void |
| `Disconnect()` | void |
| `Pair()` | void |
| `Unpair()` | void |

## BluetoothDeviceState

| Value | Number |
| --- | --- |
| `Disconnected` | 0 |
| `Connected` | 1 |

## BluetoothOperation

| Value | Number |
| --- | --- |
| `None` | 0 |
| `Connecting` | 1 |
| `Disconnecting` | 2 |
| `Pairing` | 3 |
| `Unpairing` | 4 |

## BluetoothService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Adapters` | ObservableList of [BluetoothAdapter](#bluetoothadapter), read-only |
| `DefaultAdapter` | [BluetoothAdapter](#bluetoothadapter)?, read-only |
| `Devices` | ObservableList of [BluetoothDevice](#bluetoothdevice), read-only |
| `Discovering` | bool, read-only |
| `Error` | string, read-only |
| `NearbyDevices` | ObservableList of [BluetoothDevice](#bluetoothdevice), read-only |
| `PairedDevices` | ObservableList of [BluetoothDevice](#bluetoothdevice), read-only |
| `Ready` | bool, read-only |
| `OperationFailed` | Action of string |
| `Dispose()` | void |
| `Refresh()` | void |
| `StartDiscovery()` | void |
| `StartMonitoring()` | void |
| `StopDiscovery()` | void |
| `StopMonitoring()` | void |
