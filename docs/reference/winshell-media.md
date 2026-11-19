# Winshell.Media

What is playing, and play, pause, skip and seek. Per-app volume stays on the audio session. Reach media from `Shell.Current.Media`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## MediaLoopState

| Value | Number |
| --- | --- |
| `None` | 0 |
| `Track` | 1 |
| `List` | 2 |

## MediaPlaybackState

| Value | Number |
| --- | --- |
| `Stopped` | 0 |
| `Playing` | 1 |
| `Paused` | 2 |

## MediaService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Sessions` | ObservableList of [MediaSession](#mediasession), read-only |

## MediaSession

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AppId` | string, read-only |
| `AppName` | string, read-only |
| `CanControl` | bool, read-only |
| `CanGoNext` | bool, read-only |
| `CanGoPrevious` | bool, read-only |
| `CanPause` | bool, read-only |
| `CanPlay` | bool, read-only |
| `CanSeek` | bool, read-only |
| `CanStop` | bool, read-only |
| `CanSwitchTo` | bool, read-only |
| `CanTogglePlaying` | bool, read-only |
| `IsPlaying` | bool |
| `Length` | double, read-only |
| `LengthSupported` | bool, read-only |
| `LoopState` | [MediaLoopState](#medialoopstate) |
| `LoopSupported` | bool, read-only |
| `MaxRate` | double, read-only |
| `Metadata` | IReadOnlyDictionary of string, object, read-only |
| `MinRate` | double, read-only |
| `PlaybackState` | [MediaPlaybackState](#mediaplaybackstate) |
| `Position` | double |
| `PositionSupported` | bool, read-only |
| `Rate` | double |
| `Shuffle` | bool |
| `ShuffleSupported` | bool, read-only |
| `TrackAlbum` | string, read-only |
| `TrackAlbumArtist` | string, read-only |
| `TrackArtUrl` | string, read-only |
| `TrackArtist` | string, read-only |
| `TrackTitle` | string, read-only |
| `UniqueId` | uint, read-only |
| `PostTrackChanged` | Action |
| `TrackChanged` | Action |
| `Next()` | void |
| `Pause()` | void |
| `Play()` | void |
| `Previous()` | void |
| `RefreshPosition()` | void |
| `Seek(double offset)` | void |
| `Stop()` | void |
| `SwitchTo()` | void |
| `TogglePlaying()` | void |
