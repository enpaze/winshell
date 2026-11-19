# Winshell.Windows

App windows and virtual desktops. Reach them from `Shell.Current.WindowList` and `Shell.Current.VirtualDesktops`. A preview of one window is `WindowThumbnail` in [Winshell](winshell.md#windowthumbnail).

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## AppWindow

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Activated` | bool, read-only |
| `AppId` | string, read-only |
| `Attention` | bool, read-only |
| `Desktop` | [VirtualDesktop](#virtualdesktop)?, read-only |
| `DesktopId` | string, read-only |
| `Executable` | string, read-only |
| `Fullscreen` | bool, read-only |
| `Icon` | string, read-only |
| `Maximized` | bool |
| `Minimized` | bool |
| `Parent` | [AppWindow](#appwindow)?, read-only |
| `Screens` | IReadOnlyList of [ScreenInfo](winshell.md#screeninfo), read-only |
| `Title` | string, read-only |
| `Closed` | Action |
| `Activate()` | void |
| `Close()` | void |
| `MoveTo([ScreenInfo](winshell.md#screeninfo)? screen)` | void |
| `MoveToDesktop([VirtualDesktop](#virtualdesktop)? desktop)` | void |

## VirtualDesktop

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Current` | bool, read-only |
| `Id` | string, read-only |
| `Index` | int, read-only |
| `Name` | string, read-only |

## VirtualDesktopsService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Current` | [VirtualDesktop](#virtualdesktop)?, read-only |
| `Desktops` | ObservableList of [VirtualDesktop](#virtualdesktop), read-only |
| `Supported` | bool, read-only |
| `UnsupportedReason` | string, read-only |
| `Create()` | [VirtualDesktop](#virtualdesktop)? |
| `Dispose()` | void |
| `Remove([VirtualDesktop](#virtualdesktop)? desktop)` | void |
| `SwitchTo([VirtualDesktop](#virtualdesktop)? desktop)` | void |

## WindowListService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `ActiveWindow` | [AppWindow](#appwindow)?, read-only |
| `Windows` | ObservableList of [AppWindow](#appwindow), read-only |
| `Dispose()` | void |
