# Winshell.Capture

A still or a live capture of a window or a screen. `CaptureView` in [Winshell](winshell.md#captureview) shows a session. `WindowThumbnail` is a DWM preview and does not capture.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## CaptureSession

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `CaptureSession([Log](winshell.md#log) log)` | constructor |
| `Error` | string, read-only |
| `Frame` | [PixelImage](winshell.md#pixelimage)?, read-only |
| `FrameInterval` | int |
| `HasFrame` | bool, read-only |
| `IncludeCursor` | bool |
| `PixelSize` | Size, read-only |
| `Running` | bool, read-only |
| `Target` | [CaptureTarget](#capturetarget)? |
| `FrameReady` | Action |
| `CopyToClipboard(IntPtr owner)` | bool |
| `Dispose()` | void |
| `SaveToFile(string path)` | bool |
| `Start()` | bool |
| `Stop()` | void |

## CaptureTarget

| Member | Type |
| --- | --- |
| `CaptureTarget(IntPtr Window, string MonitorId)` | constructor |
| `MonitorId` | string, init |
| `Window` | IntPtr, init |
| `ForScreen([ScreenInfo](winshell.md#screeninfo) screen)` | [CaptureTarget](#capturetarget), static |
| `ForWindow(IntPtr window)` | [CaptureTarget](#capturetarget), static |
| `ForWindow([AppWindow](winshell-appwindows.md#appwindow) window)` | [CaptureTarget](#capturetarget), static |
