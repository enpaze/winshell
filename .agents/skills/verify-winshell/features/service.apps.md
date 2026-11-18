# Apps

`shell.Apps` lists the apps that Start lists, from the Start menu's program folders and the installed packages, and keeps the list current when an app is installed or removed. A configuration uses it to show, search and launch apps, such as in a launcher. The feature shell's row `service.apps` shows `apps: N`, the number of apps in `Apps`.

## Sub-features

- `list` `Apps` holds each app that Start lists and does not hide, with its name, icon, target and whether it is packaged.
- `recent-frequent` `Recent` and `Frequent` hold the apps used most recently and most often.
- `search` `Search(query)` returns the apps that match a query, and `Find(name)` and `ById(id)` return one app.
- `launch` `AppEntry.Launch()` starts the app.
- `names` `AppsService.DisplayName(appId)`, `FallbackName(appId)` and `ProgramDescription(path)` give a name for an app or a program.
- `update` the lists change one second after the last install or removal that Windows reports.

## How to get to it (user POV)

- A configuration reads `shell.Apps`, or `Props.Shell.Apps` in a widget.
- A configuration calls the static `AppsService.DisplayName`, `AppsService.FallbackName` and `AppsService.ProgramDescription`.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the app count.** Read the `service.apps` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.apps`, and its `text` matches `^apps: [1-9][0-9]*$`, so the shell lists at least one app. The driver retries for 20 seconds.

```json
[
  {"element": "service.apps", "has": {"text": "^apps: [1-9][0-9]*$"}, "within": 20}
]
```

## Gotchas

- The shell reads the apps in the background after it first reads `shell.Apps`. Until that read ends, the row shows `apps: 0`, so the step retries for 20 seconds.
- The number depends on the PC, because each user's Start menu has its own apps. Assert at least one, never a fixed number.
- `Launch()` starts a real app on the user's desktop. No step launches one.
