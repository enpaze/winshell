# Power

`shell.Power` reads the batteries and whether the PC runs on battery: the charge, the state, the time to empty or full, and the battery's health. A shell uses it for a battery indicator. The feature shell's row `service.power` shows `on battery: <True|False>; battery: N%`, the charge of `DisplayDevice` rounded to a whole percent, or `battery: none` when the PC has no battery.

## Sub-features

- `on-battery` `OnBattery` is `True` while the PC runs on battery.
- `display-device` `DisplayDevice` is the battery to show, with `IsPresent`, `Percentage` from 0 to 1 and `State`, such as `Charging` or `FullyCharged`.
- `devices` `Devices` lists each battery as a `PowerDevice`, with its model, capacity, charge rate, `TimeToEmpty`, `TimeToFull` and health.
- `update` the values change when Windows reports a power change.

## How to get to it (user POV)

- A shell reads `shell.Power`, or `Shell.Power` in a widget.
- In the starter, the `battery` widget shows the charge at the end of the bar.
- In the omarchy-style shell, the `battery` widget shows the charge and its state, and choosing it opens `ms-settings:powersleep`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the battery.** Read the `service.power` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.power`, and its `text` matches `^on battery: (True|False); battery: ([0-9]{1,3}%|none)$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.power", "has": {"text": "^on battery: (True|False); battery: ([0-9]{1,3}%|none)$"}, "within": 20}
]
```

## Gotchas

- A desktop PC or a CI runner has no battery, so the row shows `on battery: False; battery: none`, and the step accepts that. A laptop shows its charge.
- The read runs in the background, so the row can show `battery: none` for a moment on a laptop. The step retries for 20 seconds.
- On a PC with no battery, the starter's and the omarchy-style shell's battery widgets are hidden.
