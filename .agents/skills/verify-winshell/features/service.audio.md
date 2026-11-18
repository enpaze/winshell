# Audio

`shell.Audio` reads the playback and recording devices and each app's audio session, with their volume and mute, and changes them. A configuration uses it for a volume control or a device picker. The feature shell's row `service.audio` shows `ready: <True|False>; playback: <device>; recording: <device>`, where each device is the name of the default device of that kind, or `none` when Windows has none.

## Sub-features

- `ready` `Ready` is `True` once Windows audio answers, and stays `False` when Windows audio is not available.
- `defaults` `DefaultPlaybackDevice` and `DefaultRecordingDevice` are the default devices, and setting one makes another device the default.
- `devices` `PlaybackDevices` and `RecordingDevices` list each device, with its name, form factor and channels. A device's `Sessions` lists each app's `AudioSession` on it.
- `volume` `Volume`, `Volumes` and `Muted` read and set the volume and mute of a device or a session.
- `levels` `AudioPeakMonitor` reports the peak level of a device or a session while it is enabled.

## How to get to it (user POV)

- A configuration reads `shell.Audio`, or `Shell.Audio` in a widget.
- A configuration creates `new AudioPeakMonitor(shell.Loop)` and sets its `Audio` and `Enabled`.
- In the omarchy-style shell, the `audio` widget shows the playback device and its volume, and choosing it opens `ms-settings:sound`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the default devices.** Read the `service.audio` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.audio`, and its `text` matches `^ready: (True|False); playback: .+; recording: .+$`, with a device name or `none` after `playback:` and `recording:`. The driver retries for 20 seconds.

```json
[
  {"element": "service.audio", "has": {"text": "^ready: (True|False); playback: .+; recording: .+$"}, "within": 20}
]
```

## Gotchas

- The devices depend on the PC. A PC or a CI runner with no sound device shows `playback: none` and `recording: none`, and the step accepts that.
- `ready: False` means that Windows audio did not answer when the service started. The log has an `audio` warning that says why.
- The step only reads. Setting `Volume`, `Muted` or a default device changes the user's real audio, so no step sets them.
