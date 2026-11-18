# Bluetooth

`shell.Bluetooth` reads the Bluetooth adapter and the paired and nearby devices, and turns the radio on or off, pairs, connects and disconnects. A configuration uses it for a Bluetooth control. The feature shell's row `service.bluetooth` shows `ready: <True|False>; adapter: <name>; devices: N`, where the adapter is the default adapter's name, or `none` when there is none.

## Sub-features

- `monitor` `StartMonitoring()` reads the adapter and the devices and keeps them current, `Refresh()` reads them once, and `StopMonitoring()` stops.
- `ready` `Ready` is `True` once a read has finished, and `Error` says why a read failed.
- `adapter` `DefaultAdapter` and `Adapters` have each adapter's name, address and `State`, and `Enabled`, `Discoverable` and `Pairable` change it.
- `devices` `Devices`, `PairedDevices` and `NearbyDevices` list the devices, with their name, state and battery.
- `discovery` `StartDiscovery()` and `StopDiscovery()` look for nearby devices, and `Discovering` says whether a search runs.
- `device-actions` `BluetoothDevice.Pair()`, `Unpair()`, `CancelPairing()`, `Connect()` and `Disconnect()` act on a device, and `OperationFailed` reports a failure.

## How to get to it (user POV)

- A configuration reads `shell.Bluetooth`, or `Shell.Bluetooth` in a widget, and calls `StartMonitoring()` or `Refresh()`.
- In the omarchy-style shell, the `bluetooth` widget shows whether the radio is on and which devices are connected, and choosing it opens `ms-settings:bluetooth`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the adapter.** Read the `service.bluetooth` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.bluetooth`, and its `text` matches `^ready: (True|False); adapter: .+; devices: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.bluetooth", "has": {"text": "^ready: (True|False); adapter: .+; devices: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- The service reads nothing until the configuration calls `StartMonitoring()` or `Refresh()`. The feature shell calls neither, so its row shows `ready: False; adapter: none; devices: 0` on every PC. The step proves that the row renders, not that the adapter was read.
- After a read, a PC or a CI runner with no Bluetooth adapter shows `adapter: none`, and the step accepts that.
- Nearby devices show only during discovery, and only while the radio is on.
- Setting `Enabled`, `Discoverable` or `Pairable`, pairing and connecting change the user's real Bluetooth state. No step does them.
