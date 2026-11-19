# Winshell.Tray

The icons apps add to the notification area. Reach them from `Shell.Current.Tray`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## BalloonIcon

| Value | Number |
| --- | --- |
| `None` | 0 |
| `Info` | 1 |
| `Warning` | 2 |
| `Error` | 3 |
| `App` | 4 |

## SystemTrayItem

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AppName` | string, read-only |
| `Executable` | string, read-only |
| `Icon` | [PixelImage](winshell.md#pixelimage)?, read-only |
| `Id` | string, read-only |
| `Overflow` | bool, read-only |
| `ProcessId` | int, read-only |
| `Tooltip` | string, read-only |
| `Balloon` | Action of string, string, [BalloonIcon](#balloonicon) |
| `Ready` | Action |
| `Click()` | void |
| `Display()` | void |
| `Display(int x, int y)` | void |
| `DoubleClick()` | void |
| `IconAt(int size)` | [PixelImage](winshell.md#pixelimage)? |
| `MiddleClick()` | void |

## TrayService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Items` | ObservableList of [SystemTrayItem](#systemtrayitem), read-only |
