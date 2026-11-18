# Virtual desktops

`shell.VirtualDesktops` lists the Windows virtual desktops and the current one, and creates, removes and switches desktops. A configuration uses it for a desktop switcher. The feature shell's row `service.virtual-desktops` shows `supported: <True|False>; desktops: N`.

## Sub-features

- `supported` `Supported` is `True` when this Windows version answers the virtual desktop calls, and `UnsupportedReason` says why not.
- `desktops` `Desktops` lists each `VirtualDesktop`, with its `Id`, `Index`, `Name` and whether it is `Current`.
- `current` `Current` is the desktop on screen now, and the list follows a switch made outside the shell.
- `change` `SwitchTo(desktop)`, `Create()` and `Remove(desktop)` change the desktops.
- `move-window` `AppWindow.MoveToDesktop(desktop)` moves a window to another desktop, and `AppWindow.Desktop` says where it is.

## How to get to it (user POV)

- A configuration reads `shell.VirtualDesktops`, or `Props.Shell.VirtualDesktops` in a widget.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the desktops.** Read the `service.virtual-desktops` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.virtual-desktops`, and its `text` matches `^supported: (True|False); desktops: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.virtual-desktops", "has": {"text": "^supported: (True|False); desktops: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- On a Windows version where the calls are not supported, the row shows `supported: False; desktops: 0`, and the step accepts that. Read `UnsupportedReason` or the log to learn why.
- `SwitchTo`, `Create` and `Remove` change the user's real desktops and move the windows on screen. No step calls them.
