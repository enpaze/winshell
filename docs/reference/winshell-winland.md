# Winshell.Winland

Monitors, workspaces and windows from Winland, over its named pipe. Reach them from `Shell.Current.Winland`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## WinlandEvent

| Member | Type |
| --- | --- |
| `WinlandEvent(string Name, string Data)` | constructor |
| `Data` | string, init |
| `Name` | string, init |
| `Parse(int argumentCount)` | IReadOnlyList of string |

## WinlandMonitor

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `ActiveWorkspace` | [WinlandWorkspace](#winlandworkspace)?, read-only |
| `Description` | string, read-only |
| `Focused` | bool, read-only |
| `Height` | int, read-only |
| `Id` | int, read-only |
| `LastIpcObject` | JsonElement?, read-only |
| `Name` | string, read-only |
| `Scale` | double, read-only |
| `Width` | int, read-only |
| `X` | int, read-only |
| `Y` | int, read-only |
| `ActivateWorkspace(int number)` | void |

## WinlandService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `ActiveWindow` | [WinlandWindow](#winlandwindow)?, read-only |
| `ConfigPath` | string?, read-only |
| `FocusedMonitor` | [WinlandMonitor](#winlandmonitor)?, read-only |
| `FocusedWorkspace` | [WinlandWorkspace](#winlandworkspace)?, read-only |
| `LogPath` | string?, read-only |
| `Monitors` | ObservableList of [WinlandMonitor](#winlandmonitor), read-only |
| `PipePath` | string, read-only |
| `ProcessId` | int, read-only |
| `Windows` | ObservableList of [WinlandWindow](#winlandwindow), read-only |
| `Workspaces` | ObservableList of [WinlandWorkspace](#winlandworkspace), read-only |
| `RawEvent` | Action of [WinlandEvent](#winlandevent) |
| `Dispatch(string request)` | void |
| `MonitorFor([ScreenInfo](winshell.md#screeninfo)? screen)` | [WinlandMonitor](#winlandmonitor)? |
| `RefreshMonitors()` | void |
| `RefreshWindows()` | void |
| `RefreshWorkspaces()` | void |
| `Reload()` | void |

## WinlandWindow

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Activated` | bool, read-only |
| `Address` | string, read-only |
| `Handle` | IntPtr, read-only |
| `LastIpcObject` | JsonElement?, read-only |
| `Monitor` | [WinlandMonitor](#winlandmonitor)?, read-only |
| `Title` | string, read-only |
| `Urgent` | bool, read-only |
| `Window` | [AppWindow](winshell-appwindows.md#appwindow)?, read-only |
| `Workspace` | [WinlandWorkspace](#winlandworkspace)?, read-only |

## WinlandWorkspace

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Active` | bool, read-only |
| `AppIcons` | IReadOnlyList of string, read-only |
| `Focused` | bool, read-only |
| `HasFullscreen` | bool, read-only |
| `Id` | int, read-only |
| `LastIpcObject` | JsonElement?, read-only |
| `Monitor` | [WinlandMonitor](#winlandmonitor)?, read-only |
| `Name` | string, read-only |
| `Urgent` | bool, read-only |
| `Windows` | ObservableList of [WinlandWindow](#winlandwindow), read-only |
| `Activate()` | void |
