# Jump lists

`shell.JumpLists` reads an app's jump list: the recent and frequent destinations, such as files and folders, that Windows keeps for that app. A configuration uses it to show an app's recent items, such as in a taskbar menu. The feature shell's row `service.jump-lists` reads File Explorer's list with `ForApp("Microsoft.Windows.Explorer")` and shows `explorer: <available|unavailable>; destinations: N`.

## Sub-features

- `for-app` `ForApp(appId)` returns the `AppJumpList` for an app's AppUserModelID and reads it again on each call.
- `available` `AppJumpList.Available` is `True` when Windows has a jump list for that app.
- `destinations` `AppJumpList.Destinations` lists the names of up to 10 recent and up to 10 frequent destinations, without duplicates.

## How to get to it (user POV)

- A configuration calls `shell.JumpLists.ForApp(appId)`, or `Props.Shell.JumpLists.ForApp(appId)` in a widget, with an app's AppUserModelID, such as `AppWindow.AppId` or `AppEntry.AppId`.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read File Explorer's jump list.** Read the `service.jump-lists` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.jump-lists`, and its `text` matches `^explorer: (available|unavailable); destinations: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.jump-lists", "has": {"text": "^explorer: (available|unavailable); destinations: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- The list depends on the user's history. A new account or a CI runner can have no jump list for File Explorer, which shows `explorer: unavailable; destinations: 0`, and the step accepts that.
- `ForApp` reads the list when it is called, and the list does not update by itself. The feature shell calls it once at start, so the row keeps that first read. Call `ForApp` again to read it fresh.
- An empty or blank app id gives a list that is not available.
