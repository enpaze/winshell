# Network

`shell.Network` reads connectivity, the network adapters, the Wi-Fi networks in range and the connection profiles, and connects to Wi-Fi networks. A shell uses it for a network indicator or a Wi-Fi picker. The feature shell's row `service.network` shows `ready: <True|False>; connectivity: <level>; devices: N`, where the level is a `NetworkConnectivity` value: `Unknown`, `None`, `CaptivePortal`, `LocalAccess` or `InternetAccess`.

## Sub-features

- `ready` `Ready` is `True` once the first read has finished, and `Error` says why a read failed.
- `connectivity` `Connectivity` says whether the PC reaches the internet, only the local network, or a captive portal.
- `devices` `Devices` lists each Wi-Fi and wired adapter, with its state and its `Networks`.
- `profiles` `Profiles` lists the connection profiles, with their name, cost, metered state and signal strength.
- `wifi-radio` `WifiEnabled`, `WifiHardwareEnabled`, `WifiState` and `AirplaneMode` read the radios, and setting `WifiEnabled` turns Wi-Fi on or off.
- `wifi-access` `RequestAccess()` asks Windows for Wi-Fi access, and `AccessStatus` and `RequestingAccess` show the answer.
- `wifi-connect` `WifiDevice.Scan()` looks for networks, and `WifiNetwork.Connect()`, `ConnectWithPassword(password)`, `Disconnect()` and `Forget()` act on one.
- `refresh` `Refresh()` reads everything again.

## How to get to it (user POV)

- A shell reads `shell.Network`, or `Shell.Network` in a widget.
- In the omarchy-style shell, the `network` widget names the connection and shows its type and signal, and choosing it opens `ms-settings:network-status`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the connectivity.** Read the `service.network` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.network`, and its `text` matches `^ready: (True|False); connectivity: [A-Za-z]+; devices: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.network", "has": {"text": "^ready: (True|False); connectivity: [A-Za-z]+; devices: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- The read runs in the background, so the row can show `ready: False; connectivity: Unknown` for a moment. The step retries for 20 seconds.
- Without Wi-Fi access, the adapters and the connection still show, and the networks in range do not. Wi-Fi access also needs location services on.
- Setting `WifiEnabled`, connecting and forgetting change the user's real network. No step does them.
