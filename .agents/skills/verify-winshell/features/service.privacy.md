# Privacy

`shell.Privacy` lists the apps that use the microphone, the camera or location right now. A shell uses it for a privacy indicator. The feature shell's row `service.privacy` shows `microphone: N; camera: N; location: N`, the number of apps using each now.

## Sub-features

- `microphone` `Microphone` lists the apps using the microphone, and `MicrophoneInUse` is `True` while any app uses it.
- `camera` `Camera` lists the apps using the camera, and `CameraInUse` is `True` while any app uses it.
- `location` `Location` lists the apps using location, and `LocationInUse` is `True` while any app uses it.
- `app` each `PrivacyApp` has its `Name`, `Path`, whether it is `Packaged`, and `ThisProgram`, which is `True` for the shell itself.
- `watching` the service watches while something follows its `PropertyChanged`.

## How to get to it (user POV)

- A shell reads `shell.Privacy`, or `Shell.Privacy` in a widget, and follows it with `UseObservable` or `PropertyChanged`.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the counts.** Read the `service.privacy` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.privacy`, and its `text` matches `^microphone: [0-9]+; camera: [0-9]+; location: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.privacy", "has": {"text": "^microphone: [0-9]+; camera: [0-9]+; location: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- The counts are what other apps do now, such as a video call. With nothing in use, or on a CI runner, the row shows all zeros, and the step accepts any count.
- Nothing in the run uses the microphone, the camera or location, so the step does not prove that an app in use is listed.
