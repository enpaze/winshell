# System tray

`shell.SystemTray` lists the icons that apps add to the notification area, and passes the user's clicks to them. A configuration uses it to show tray icons in its own bar. The feature shell's row `service.system-tray` shows `icons: N`, the number of icons in `Items`.

## Sub-features

- `items` `Items` lists each `SystemTrayItem`, with its app name, program, tooltip, icon and whether it is in the `Overflow` behind the chevron.
- `icon` `IconAt(size)` returns the icon at a size, as a `PixelImage`.
- `click` `Click()`, `DoubleClick()` and `MiddleClick()` pass a click to the app, and `Display()` asks the app for its menu.
- `balloon` `Balloon` raises when an app shows a balloon message, with its title, text and `BalloonIcon`.
- `host` one Winshell instance in the Windows session hosts the tray icons, and asks apps to add their icons again when it starts.

## How to get to it (user POV)

- A configuration reads `shell.SystemTray.Items`, or `Props.Shell.SystemTray.Items` in a widget.
- In the omarchy-style shell, the `tray` widget shows the icons on the primary screen's bar, and the user chooses an icon to click it.
- The user chooses which icons show in Windows Settings > Personalization > Taskbar > Other system tray icons.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the icon count.** Read the `service.system-tray` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.system-tray`, and its `text` matches `^icons: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.system-tray", "has": {"text": "^icons: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- Only one Winshell instance in the Windows session hosts the tray. While another one, such as the user's own shell, hosts it, this shell waits and its row shows `icons: 0`. The step accepts any count.
- Some apps add their icon only when they start, and never again when a new host asks. Those icons are missing until the app starts again.
- The volume, network and battery icons are Explorer's own, not tray icons, so they are not in `Items`.
- `Click()` and the other actions act on the user's real apps. No step calls them.
