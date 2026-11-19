# Window list

`shell.WindowList` lists the open app windows that a taskbar would show, and the active one, and acts on them. A shell uses it for a taskbar or a window switcher. The feature shell's row `service.window-list` shows `windows: N; active: <yes|none>`, where `active` says whether `ActiveWindow` is set.

## Sub-features

- `windows` `Windows` lists each visible, titled top-level window that would show on a taskbar, as an `AppWindow`.
- `active` `ActiveWindow` is the window in the foreground, or none.
- `window-facts` each `AppWindow` has its `Title`, `AppId`, `Executable`, `Icon`, `Screens`, `Desktop`, and whether it is `Minimized`, `Maximized`, `Fullscreen` or wants `Attention`.
- `window-actions` `Activate()`, `Close()`, `MoveTo(screen)` and `MoveToDesktop(desktop)` act on a window, and setting `Minimized` or `Maximized` changes it.
- `closed` `AppWindow.Closed` raises when a window closes.
- `thumbnail` `WindowThumbnail` shows a live preview of an `AppWindow`.

## How to get to it (user POV)

- A shell reads `shell.WindowList`, or `Shell.WindowList` in a widget.
- A shell sets `WindowThumbnail.Window` to an `AppWindow` to preview it.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the windows.** Read the `service.window-list` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.window-list`, and its `text` matches `^windows: [1-9][0-9]*; active: (yes|none)$`, so at least one window is listed, such as the feature shell's own `Winshell features` window. The driver retries for 20 seconds.

```json
[
  {"element": "service.window-list", "has": {"text": "^windows: [1-9][0-9]*; active: (yes|none)$"}, "within": 20}
]
```

## Gotchas

- The count includes the user's own windows, so it changes as they work. Assert at least one, never a fixed number.
- `active: none` is a normal state, such as when the desktop has focus. The step accepts it.
- `Activate()`, `Close()` and the move actions act on the user's real windows. No step calls them.
