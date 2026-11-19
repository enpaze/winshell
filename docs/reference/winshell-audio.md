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

## AudioDevice

Has the members of [AudioVolume](#audiovolume).

| Member | Type |
| --- | --- |
| `FormFactor` | [AudioFormFactor](#audioformfactor), read-only |
| `Sessions` | ObservableList of [AudioSession](#audiosession), read-only |

## AudioFormFactor

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `Speakers` | 1 |
| `Headphones` | 2 |
| `Headset` | 3 |
| `Handset` | 4 |
| `Microphone` | 5 |
| `Line` | 6 |
| `Spdif` | 7 |
| `Hdmi` | 8 |
| `Network` | 9 |

## AudioPeakMonitor

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AudioPeakMonitor()` | constructor |
| `Audio` | [AudioVolume](#audiovolume)? |
| `Channels` | IReadOnlyList of [AudioChannel](#audiochannel), read-only |
| `Enabled` | bool |
| `Peak` | double, read-only |
| `Peaks` | IReadOnlyList of double, read-only |
| `Dispose()` | void |

## AudioService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `DefaultPlaybackDevice` | [PlaybackDevice](#playbackdevice)? |
| `DefaultRecordingDevice` | [RecordingDevice](#recordingdevice)? |
| `PlaybackDevices` | ObservableList of [PlaybackDevice](#playbackdevice), read-only |
| `Ready` | bool, read-only |
| `RecordingDevices` | ObservableList of [RecordingDevice](#recordingdevice), read-only |

## AudioSession

Has the members of [AudioVolume](#audiovolume).

| Member | Type |
| --- | --- |
| `Active` | bool, read-only |
| `Device` | [AudioDevice](#audiodevice), read-only |

## AudioVolume

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Channels` | IReadOnlyList of [AudioChannel](#audiochannel), read-only |
| `Muted` | bool |
| `Name` | string, read-only |
| `Volume` | double |
| `Volumes` | IReadOnlyList of double |

## PlaybackDevice

Has the members of [AudioDevice](#audiodevice).


## RecordingDevice

Has the members of [AudioDevice](#audiodevice).
