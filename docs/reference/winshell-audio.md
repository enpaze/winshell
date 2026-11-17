# Winshell.Audio

Playback and recording devices, sessions, volume, mute and levels. Reach them from `Shell.Current.Audio`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## AudioChannel

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `Mono` | 1 |
| `FrontLeft` | 2 |
| `FrontRight` | 3 |
| `FrontCenter` | 4 |
| `LowFrequency` | 5 |
| `BackLeft` | 6 |
| `BackRight` | 7 |
| `FrontLeftOfCenter` | 8 |
| `FrontRightOfCenter` | 9 |
| `BackCenter` | 10 |
| `SideLeft` | 11 |
| `SideRight` | 12 |
| `TopCenter` | 13 |
| `TopFrontLeft` | 14 |
| `TopFrontCenter` | 15 |
| `TopFrontRight` | 16 |
| `TopBackLeft` | 17 |
| `TopBackCenter` | 18 |
| `TopBackRight` | 19 |

## AudioNode

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Active` | bool, read-only |
| `Channels` | IReadOnlyList of [AudioChannel](#audiochannel), read-only |
| `Description` | string, read-only |
| `Device` | [AudioNode](#audionode)?, read-only |
| `Id` | int, read-only |
| `InterfaceName` | string, read-only |
| `IsPlayback` | bool, read-only |
| `IsSession` | bool, read-only |
| `Muted` | bool |
| `Name` | string, read-only |
| `Properties` | IReadOnlyDictionary of string, string, read-only |
| `Volume` | double |
| `Volumes` | IReadOnlyList of double |

## AudioPeakMonitor

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AudioPeakMonitor([IEventLoop](winshell.md#ieventloop) loop)` | constructor |
| `Channels` | IReadOnlyList of [AudioChannel](#audiochannel), read-only |
| `Enabled` | bool |
| `Node` | [AudioNode](#audionode)? |
| `Peak` | double, read-only |
| `Peaks` | IReadOnlyList of double, read-only |
| `Dispose()` | void |

## AudioProperty

| Member | Type |
| --- | --- |
| `Adapter` | string, static |
| `AppName` | string, static |
| `EndpointId` | string, static |
| `Flow` | string, static |
| `FormFactor` | string, static |
| `FriendlyName` | string, static |
| `Icon` | string, static |
| `InterfaceName` | string, static |
| `ProcessId` | string, static |
| `ProcessName` | string, static |
| `SessionId` | string, static |

## AudioService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `DefaultPlaybackDevice` | [AudioNode](#audionode)? |
| `DefaultRecordingDevice` | [AudioNode](#audionode)? |
| `Nodes` | ObservableList of [AudioNode](#audionode), read-only |
| `Ready` | bool, read-only |
| `Dispose()` | void |
