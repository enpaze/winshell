# Winland

`shell.Winland` reads the monitors, workspaces and windows of Winland, the tiling window manager, over Winland's named pipe, and sends it requests. A shell uses it for a workspace switcher. The feature shell's row `service.winland` shows `running: <True|False>; workspaces: N`, where `running` is `True` while `ProcessId` names a running Winland.

## Sub-features

- `connection` the service connects to the pipe `winland-<session id>`, then `winland`, or the pipe that `WINLAND_PIPE` names, and `ProcessId` is 0 while no Winland answers.
- `workspaces` `Workspaces` lists each `WinlandWorkspace`, with its `Id`, `Name`, monitor, windows and whether it is `Active`, `Focused` or `Urgent`.
- `monitors` `Monitors`, `FocusedMonitor` and `MonitorFor(screen)` give each Winland monitor and its `ActiveWorkspace`.
- `windows` `Windows` and `ActiveWindow` list Winland's windows and link each one to its `AppWindow`.
- `app-icons` `WinlandWorkspace.AppIcons` gives the icon path of each app open on the workspace, once per app, in the order of its windows, for `ShellImage.Load`.
- `activate` `WinlandWorkspace.Activate()` and `WinlandMonitor.ActivateWorkspace(number)` switch workspaces.
- `dispatch` `Dispatch(request)` sends a Winland action, such as `workspace 3`, `RawEvent` raises each event with its name and JSON, and `Reload()` reloads Winland's configuration.

## How to get to it (user POV)

- A shell reads `shell.Winland`, or `Shell.Winland` in a widget.
- In the omarchy-style shell, the `workspaces` widget shows the workspaces of the bar's monitor and the icons of the apps on each, and the user chooses one to switch to it.
- The user sets `WINLAND_PIPE` to connect to another pipe.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read Winland's state.** Read the `service.winland` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.winland`, and its `text` matches `^running: (True|False); workspaces: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.winland", "has": {"text": "^running: (True|False); workspaces: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- CI has no Winland, so the row shows `running: False; workspaces: 0`, and the step accepts that. On a desktop with Winland, the row shows its workspaces.
- With Winland running, follow Live UI with Winland running in the development guide. Never call `Reload()`, `Dispatch` or a workspace switch during a run, because they change the user's real Winland.
- Without Winland, the omarchy-style `workspaces` widget is hidden.
- The service asks `j/get monitors`, `j/get workspaces`, `j/get windows` and follows `j/events`. A Winland from before `get` and JSON events answers `error: unknown command`, the shell log warns once per query that Winshell cannot read it, and the workspaces show no active one. `winland -j get workspaces` in a terminal shows what Winland reports.
