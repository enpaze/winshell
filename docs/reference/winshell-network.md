# Winshell.Network

Adapters, Wi-Fi networks, connection profiles and connectivity. Reach them from `Shell.Current.Networking`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## AirplaneModeState

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `Off` | 1 |
| `On` | 2 |

## ConnectionFailReason

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `InvalidPassword` | 1 |
| `Timeout` | 2 |
| `NetworkUnavailable` | 3 |
| `UnsupportedAuthentication` | 4 |

## ConnectionState

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `Connecting` | 1 |
| `Connected` | 2 |
| `Disconnecting` | 3 |
| `Disconnected` | 4 |

## DeviceType

| Value | Number |
| --- | --- |
| `None` | 0 |
| `Wifi` | 1 |
| `Wired` | 2 |

## Network

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Connected` | bool, read-only |
| `Device` | [NetworkDevice](#networkdevice), read-only |
| `Name` | string, read-only |
| `Saved` | bool, read-only |
| `SavedProfiles` | ObservableList of [WifiProfile](#wifiprofile), read-only |
| `State` | [ConnectionState](#connectionstate), read-only |

## NetworkAccessStatus

| Value | Number |
| --- | --- |
| `Unspecified` | 0 |
| `Allowed` | 1 |
| `DeniedByUser` | 2 |
| `DeniedBySystem` | 3 |

## NetworkConnectivity

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `None` | 1 |
| `CaptivePortal` | 2 |
| `LocalAccess` | 3 |
| `InternetAccess` | 4 |

## NetworkCost

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `Unrestricted` | 1 |
| `Fixed` | 2 |
| `Variable` | 3 |

## NetworkDevice

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Address` | string, read-only |
| `Connected` | bool, read-only |
| `Id` | string, read-only |
| `Name` | string, read-only |
| `Networks` | ObservableList of [Network](#network), read-only |
| `State` | [ConnectionState](#connectionstate), read-only |
| `Type` | [DeviceType](#devicetype), read-only |

## NetworkProfile

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AdapterId` | string, read-only |
| `Connectivity` | [NetworkConnectivity](#networkconnectivity), read-only |
| `Cost` | [NetworkCost](#networkcost), read-only |
| `Id` | string, read-only |
| `Metered` | bool, read-only |
| `Name` | string, read-only |
| `OverDataLimit` | bool, read-only |
| `Roaming` | bool, read-only |
| `SignalAvailable` | bool, read-only |
| `SignalStrength` | double, read-only |

## NetworkingService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AccessStatus` | [NetworkAccessStatus](#networkaccessstatus), read-only |
| `AirplaneMode` | [AirplaneModeState](#airplanemodestate), read-only |
| `Connectivity` | [NetworkConnectivity](#networkconnectivity), read-only |
| `Devices` | ObservableList of [NetworkDevice](#networkdevice), read-only |
| `Error` | string, read-only |
| `Profiles` | ObservableList of [NetworkProfile](#networkprofile), read-only |
| `Ready` | bool, read-only |
| `RequestingAccess` | bool, read-only |
| `WifiEnabled` | bool |
| `WifiHardwareEnabled` | bool, read-only |
| `WifiState` | [WifiRadioState](#wifiradiostate), read-only |
| `OperationFailed` | Action of string |
| `Refresh()` | void |
| `RequestAccess()` | void |

## WifiDevice

Has the members of [NetworkDevice](#networkdevice).

| Member | Type |
| --- | --- |
| `Error` | string, read-only |
| `Mode` | [WifiDeviceMode](#wifidevicemode), read-only |
| `Scanning` | bool, read-only |
| `OperationFailed` | Action of string |
| `Scan()` | void |

## WifiDeviceMode

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `AccessPoint` | 1 |
| `Mesh` | 2 |
| `Station` | 3 |
| `AdHoc` | 4 |

## WifiNetwork

Has the members of [Network](#network).

| Member | Type |
| --- | --- |
| `Busy` | bool, read-only |
| `Error` | string, read-only |
| `Security` | [WifiSecurityType](#wifisecuritytype), read-only |
| `SignalStrength` | double, read-only |
| `OperationFailed` | Action of string |
| `Connect()` | void |
| `ConnectWithPassword(string password)` | void |
| `ConnectWithProfile([WifiProfile](#wifiprofile)? profile)` | void |
| `Disconnect()` | void |
| `Forget()` | void |

## WifiProfile

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AutoConnect` | bool |
| `Error` | string, read-only |
| `HasPassword` | bool, read-only |
| `Id` | string, read-only |
| `Loaded` | bool, read-only |
| `Name` | string, read-only |
| `Security` | [WifiSecurityType](#wifisecuritytype), read-only |
| `Ssid` | string, read-only |
| `OperationFailed` | Action of string |
| `ClearPassword()` | void |
| `Forget()` | void |

## WifiRadioState

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `On` | 1 |
| `Off` | 2 |
| `Disabled` | 3 |

## WifiSecurityType

| Value | Number |
| --- | --- |
| `Wpa3SuiteB192` | 0 |
| `Sae` | 1 |
| `Wpa2Eap` | 2 |
| `Wpa2Psk` | 3 |
| `WpaEap` | 4 |
| `WpaPsk` | 5 |
| `StaticWep` | 6 |
| `Owe` | 7 |
| `Open` | 8 |
| `Unknown` | 9 |

## WiredDevice

Has the members of [NetworkDevice](#networkdevice).

| Member | Type |
| --- | --- |
| `HasLink` | bool, read-only |
| `LinkSpeed` | int, read-only |

## WiredNetwork

Has the members of [Network](#network).
