# Brightness

`shell.Brightness` lists the monitors whose brightness Windows can read, the built-in panel of a laptop and each external monitor that answers, and sets their brightness. A shell uses it for a brightness slider. The feature shell's row `service.brightness` shows `ready: <True|False>; monitors: N`.

## Sub-features

- `ready` `Ready` is `True` once the first read has finished.
- `monitors` `Monitors` lists each `BrightnessMonitor` with its name, device name, `Connection` (`Internal` or `External`), `Minimum` and `Maximum`.
- `set` setting `BrightnessMonitor.Brightness` changes the brightness of that monitor.
- `update` the list follows changes to the screens and to the brightness of the built-in panel.

## How to get to it (user POV)

- A shell reads `shell.Brightness`, or `Shell.Brightness` in a widget.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the monitors.** Read the `service.brightness` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.brightness`, and its `text` matches `^ready: (True|False); monitors: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.brightness", "has": {"text": "^ready: (True|False); monitors: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- The count depends on the hardware. An external monitor that does not answer brightness requests is not listed, and a CI runner, which is a virtual machine, can show `monitors: 0`. The step accepts any count.
- The read runs in the background, so the row can show `ready: False` for a moment. The step retries for 20 seconds.
- Setting `Brightness` changes the user's real screen. No step sets it.
