# Idle monitor (NewIdleMonitor)

`shell.NewIdleMonitor()` makes an `IdleMonitor` that says when the user has not touched the keyboard or the mouse for `Timeout` seconds. A configuration uses it to dim the screen, lock or hide a bar after a while. The feature shell makes one with a `Timeout` of 600 seconds and turns it on, and its row `service.new-idle-monitor` shows `enabled: True; idle: <True|False>; timeout: 600`.

## Sub-features

- `timeout` `Timeout` is the number of seconds without input after which the user counts as idle.
- `is-idle` `IsIdle` turns `True` when that time has passed, and `False` at the next input.
- `enabled` `Enabled` turns the watch on or off, and `IsIdle` is `False` while it is off or while `Timeout` is 0.
- `keep-awake` with `RespectKeepAwake` on, the default, the user does not count as idle while a `KeepAwake` is enabled.

## How to get to it (user POV)

- A configuration calls `shell.NewIdleMonitor()`, sets `Timeout`, and follows `IsIdle`.
- A configuration creates `new KeepAwake(shell.Log)` and sets `Enabled` to keep the PC awake.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the idle state.** Read the `service.new-idle-monitor` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.new-idle-monitor`, and its `text` matches `^enabled: True; idle: (True|False); timeout: 600$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.new-idle-monitor", "has": {"text": "^enabled: True; idle: (True|False); timeout: 600$"}, "within": 20}
]
```

## Gotchas

- `idle` depends on the last real input in the Windows session. On an unattended desktop or a CI runner, it is `True` after 10 minutes without input. The step accepts both states.
- `winshell inspect` reads the tree without input, so a verification run does not reset the idle time. Moving the mouse does.
- `Timeout` is in seconds, not milliseconds.
