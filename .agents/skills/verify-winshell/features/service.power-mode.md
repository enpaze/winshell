# Power mode

`shell.PowerMode` reads and sets the Windows power mode, a slider with three positions from best efficiency to best performance. A configuration uses it for a power mode switch. The feature shell's row `service.power-mode` shows `ready: <True|False>; mode: <level>; available: <True|False>`, where the level is a `PowerModeLevel` value: `BestEfficiency`, `Balanced`, `BestPerformance` or `Unknown`.

## Sub-features

- `ready` `Ready` is `True` once the first read has finished.
- `available` `Available` is `True` when Windows answered the read of the power mode.
- `mode` `Mode` is the slider position, and setting it to `BestEfficiency`, `Balanced` or `BestPerformance` changes the mode.
- `effective` `Effective` is the mode Windows applies now, such as `EnergySaver`, `GameMode` or `HighPerformance`.

## How to get to it (user POV)

- A configuration reads `shell.PowerMode`, or `Shell.PowerMode` in a widget.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the power mode.** Read the `service.power-mode` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.power-mode`, and its `text` matches `^ready: (True|False); mode: [A-Za-z]+; available: (True|False)$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.power-mode", "has": {"text": "^ready: (True|False); mode: [A-Za-z]+; available: (True|False)$"}, "within": 20}
]
```

## Gotchas

- When Windows does not answer the read, the row shows `mode: Unknown; available: False`. The step accepts that.
- Setting `Mode` to `Unknown` is refused, and the log has a `power` warning.
- Setting `Mode` changes the user's real power mode. No step sets it.
