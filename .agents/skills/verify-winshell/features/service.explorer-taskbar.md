# Explorer taskbar

`shell.ExplorerTaskbar` hides Explorer's taskbar while the shell runs, so that the shell's own bar is the only one, and puts it back when the shell exits. The feature shell's row `service.explorer-taskbar` shows `hidden: False`, because the feature shell reads `Hidden` and never sets it.

## Sub-features

- `hide` setting `Hidden = true` hides Explorer's taskbar window and releases the screen edge it reserved.
- `restore` setting `Hidden = false`, quitting, Ctrl+C, `winshell kill` or a crash puts the taskbar back and reserves its edge again.
- `hand-off` a shell that is replaced leaves the taskbar hidden for the next shell, which puts it back when it exits.
- `recovery` when only the shell ends, such as after a native crash or `winshell kill --force`, `winshell run` puts the taskbar back.

## How to get to it (user POV)

- A configuration sets `shell.ExplorerTaskbar.Hidden = true` in `Start`.
- The omarchy-style shell sets it, so its bar replaces Explorer's taskbar.
- `winshell kill` ends a shell and puts the taskbar back.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the setting.** Read the `service.explorer-taskbar` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.explorer-taskbar`, and its `text` matches `^hidden: False$`, because the feature shell does not hide the taskbar. The driver retries for 20 seconds.

```json
[
  {"element": "service.explorer-taskbar", "has": {"text": "^hidden: False$"}, "within": 20}
]
```

## Gotchas

- `Hidden` is what this shell asked for, not whether the taskbar is on screen. After the omarchy-style shell is replaced, the taskbar can stay hidden for the next shell while this row shows `hidden: False`.
- The step proves only the read. Hiding the taskbar is proved by the omarchy-style shell, which hides it while its features run.
- When `winshell run` is ended together with the shell, as `taskkill /F /T` does, nothing puts the taskbar back. Restart Windows Explorer from Task Manager, or sign out and back in.
- Windows keeps some hotkeys for its own taskbar while Explorer runs, such as `Win+T` and `Win+1` through `Win+9`.
