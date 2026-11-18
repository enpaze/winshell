# Winshell

The configuration's program uses these types. `Shell.Run(shell => { ... })` starts one, from top-level statements. Windows are `PanelWindow`, `PopupWindow` and `FloatingWindow`. Their content is any WinUI element. Services hang off `Shell.Current`: [apps](winshell-apps.md), [audio](winshell-audio.md), [Bluetooth](winshell-bluetooth.md), [capture](winshell-capture.md), the [Explorer taskbar](winshell-explorertaskbar.md), [media](winshell-media.md), [network](winshell-network.md), [notifications](winshell-notifications.md), [power](winshell-power.md), [session](winshell-session.md), the [tray](winshell-tray.md), [windows](winshell-windows.md) and [Winland](winshell-winland.md). `Shell.Ipc` is what `winshell ipc` calls. The user's choices are in [Preferences](#preferences).

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## Bar

| Member | Type |
| --- | --- |
| `Bar()` | constructor |
| `Center` | ImmutableArray of [Placement](#placement), init |
| `Edge` | [Edges](#edges), init |
| `End` | ImmutableArray of [Placement](#placement), init |
| `Name` | string, init |
| `Padding` | double?, init |
| `Reserve` | bool, init |
| `Start` | ImmutableArray of [Placement](#placement), init |
| `Style` | [ShellStyle](#shellstyle)?, init |
| `Thickness` | double?, init |
| `Dispose()` | void |

## CaptureView

A WinUI Panel. The members here are the ones Winshell adds.

| Member | Type |
| --- | --- |
| `CaptureView()` | constructor |
| `Session` | [CaptureSession](winshell-capture.md#capturesession)? |
| `SessionProperty` | DependencyProperty, static |

## ColorScheme

| Value | Number |
| --- | --- |
| `System` | 0 |
| `Light` | 1 |
| `Dark` | 2 |

## Edges

| Value | Number |
| --- | --- |
| `None` | 0 |
| `Top` | 1 |
| `Left` | 2 |
| `Right` | 4 |
| `Bottom` | 8 |

## Elements

| Member | Type |
| --- | --- |
| `Alert(Element element, bool on = true)` | Element, static |
| `Surface(Element? content = null, Thickness padding = default, [ShellStyle](#shellstyle)? style = null)` | Element, static |
| `WidgetArea(string name = "", ImmutableArray of [Placement](#placement)? defaults = null, Orientation orientation = Orientation.Horizontal, [ShellStyle](#shellstyle)? style = null)` | Element, static |

## FloatingWindow

Has the members of [ShellWindow](#shellwindow).

| Member | Type |
| --- | --- |
| `FloatingWindow()` | constructor |
| `Fullscreen` | bool |
| `Maximized` | bool |
| `MaximumSize` | Size |
| `Minimized` | bool |
| `MinimumSize` | Size |
| `Owner` | [FloatingWindow](#floatingwindow)? |
| `Title` | string |

## FloatingWindow of T

Has the members of [FloatingWindow](#floatingwindow).

| Member | Type |
| --- | --- |
| `FloatingWindow<T>()` | constructor |

## FloatingWindow of T, TProps

Has the members of [FloatingWindow](#floatingwindow).

| Member | Type |
| --- | --- |
| `FloatingWindow<T, TProps>(TProps? props)` | constructor |

## IEventLoop

| Member | Type |
| --- | --- |
| `Time` | TimeProvider, read-only |
| `Post(Action action)` | void |
| `Schedule(TimeSpan delay, Action action)` | IDisposable |

## IpcException

An exception.

| Member | Type |
| --- | --- |
| `IpcException(string message)` | constructor |

## IpcSignal

| Member | Type |
| --- | --- |
| `Name` | string, read-only |
| `Emit(string value = "")` | void |

## Launcher

Has the members of [ObservableObject](#observableobject).

| Member | Type |
| --- | --- |
| `Launcher()` | constructor |
| `Commands` | ImmutableArray of [LauncherCommand](#launchercommand), init |
| `Hotkey` | string, init |
| `Name` | string, init |
| `Visible` | bool, read-only |
| `Dispose()` | void |
| `Hide()` | void |
| `Show()` | void |
| `Toggle()` | void |

## LauncherCommand

| Member | Type |
| --- | --- |
| `LauncherCommand(string Name, Action Run)` | constructor |
| `Name` | string, init |
| `Run` | Action, init |

## Log

| Member | Type |
| --- | --- |
| `Default` | [Log](#log), static |
| `Debug(string category, string message)` | void |
| `Error(string category, string message)` | void |
| `Info(string category, string message)` | void |
| `Warn(string category, string message)` | void |
| `Write([LogLevel](#loglevel) level, string category, string message)` | void |

## LogLevel

| Value | Number |
| --- | --- |
| `Debug` | 0 |
| `Info` | 1 |
| `Warn` | 2 |
| `Error` | 3 |
| `Fatal` | 4 |

## ObservableList of T

| Member | Type |
| --- | --- |
| `Count` | int, read-only |
| `Item` | T?, read-only |
| `CollectionChanged` | NotifyCollectionChangedEventHandler |
| `PropertyChanged` | PropertyChangedEventHandler |
| `Contains(T? item)` | bool |
| `GetEnumerator()` | IEnumerator of T |
| `IndexOf(T? item)` | int |

## ObservableObject

| Member | Type |
| --- | --- |
| `PropertyChanged` | PropertyChangedEventHandler |
| `Changed(string property = "")` | void, protected |
| `Changed(bool changed, string property)` | void, protected |
| `Read<T>(T? value, string property = "")` | T?, protected |
| `Set<T>(ref T? field, T? value, string property = "")` | bool, protected |

## PanelWindow

Has the members of [ShellWindow](#shellwindow).

| Member | Type |
| --- | --- |
| `PanelWindow()` | constructor |
| `AboveWindows` | bool |
| `Anchors` | [Edges](#edges) |
| `CoversScreen` | bool |
| `Focusable` | bool |
| `Margins` | Thickness |
| `ReservedSize` | double |

## PanelWindow of T

Has the members of [PanelWindow](#panelwindow).

| Member | Type |
| --- | --- |
| `PanelWindow<T>()` | constructor |

## PanelWindow of T, TProps

Has the members of [PanelWindow](#panelwindow).

| Member | Type |
| --- | --- |
| `PanelWindow<T, TProps>(TProps? props)` | constructor |

## PixelImage

| Member | Type |
| --- | --- |
| `PixelImage(int width, int height, byte[] bgra, bool premultiplied = false)` | constructor |
| `Bgra` | ReadOnlySpan of byte, read-only |
| `Height` | int, read-only |
| `Premultiplied` | bool, read-only |
| `Width` | int, read-only |
| `EncodePng()` | byte[] |

## PixelImageExtensions

| Member | Type |
| --- | --- |
| `ToImageSource([PixelImage](#pixelimage) image)` | ImageSource, static |

## Placement

| Member | Type |
| --- | --- |
| `Placement(string Widget, IReadOnlyDictionary of string, object? Settings = null)` | constructor |
| `Settings` | IReadOnlyDictionary of string, object?, init |
| `Widget` | string, init |
| `Check(object value)` | string, static |
| `Format(ImmutableArray of [Placement](#placement) placements)` | IReadOnlyList of object, static |
| `Parse(object items)` | ImmutableArray of [Placement](#placement), static |

## PopupPosition

| Member | Type |
| --- | --- |
| `Center` | [PopupPosition](#popupposition), static, read-only |
| `Top` | [PopupPosition](#popupposition), static, read-only |
| `At(Point origin)` | [PopupPosition](#popupposition), static |

## PopupWindow

Has the members of [ShellWindow](#shellwindow).

| Member | Type |
| --- | --- |
| `PopupWindow()` | constructor |
| `GrabFocus` | bool |
| `Position` | [PopupPosition](#popupposition) |
| `Dismissed` | Action |

## PopupWindow of T

Has the members of [PopupWindow](#popupwindow).

| Member | Type |
| --- | --- |
| `PopupWindow<T>()` | constructor |

## PopupWindow of T, TProps

Has the members of [PopupWindow](#popupwindow).

| Member | Type |
| --- | --- |
| `PopupWindow<T, TProps>(TProps? props)` | constructor |

## Preferences

Has the members of [ObservableObject](#observableobject).

| Member | Type |
| --- | --- |
| `Dispose()` | void |

## ScreenInfo

| Member | Type |
| --- | --- |
| `ScreenInfo(string Id, string Name, string Model, string Manufacturer, string SerialNumber, Rect Geometry, Rect WorkArea, double DevicePixelRatio, double PhysicalWidthMm, double PhysicalHeightMm, int RefreshRate, bool IsPrimary)` | constructor |
| `DevicePixelRatio` | double, init |
| `Geometry` | Rect, init |
| `Id` | string, init |
| `IsPrimary` | bool, init |
| `Manufacturer` | string, init |
| `Model` | string, init |
| `Name` | string, init |
| `PhysicalHeightMm` | double, init |
| `PhysicalWidthMm` | double, init |
| `RefreshRate` | int, init |
| `SerialNumber` | string, init |
| `WorkArea` | Rect, init |

## ScreenWindows

| Member | Type |
| --- | --- |
| `ScreenWindows(string name, Func of [ScreenInfo](#screeninfo), IReadOnlyDictionary of string, object, IDisposable make)` | constructor |
| `Dispose()` | void |

## Shell

| Member | Type |
| --- | --- |
| `AccentColor` | string |
| `Apps` | [AppsService](winshell-apps.md#appsservice), read-only |
| `Audio` | [AudioService](winshell-audio.md#audioservice), read-only |
| `Bluetooth` | [BluetoothService](winshell-bluetooth.md#bluetoothservice), read-only |
| `Brightness` | [BrightnessService](winshell-power.md#brightnessservice), read-only |
| `CacheDir` | string, read-only |
| `ColorScheme` | [ColorScheme](#colorscheme) |
| `ConfigDir` | string, read-only |
| `ConfigRoot` | string, read-only |
| `Cpu` | [CpuService](winshell-cpu.md#cpuservice), read-only |
| `Current` | [Shell](#shell), static, read-only |
| `DataDir` | string, read-only |
| `ExplorerTaskbar` | [ExplorerTaskbarService](winshell-explorertaskbar.md#explorertaskbarservice), read-only |
| `Ipc` | [ShellIpc](#shellipc), read-only |
| `IsDark` | bool, read-only |
| `JumpLists` | [JumpListService](winshell-apps.md#jumplistservice), read-only |
| `KeyboardLayouts` | [KeyboardLayoutService](winshell-keyboard.md#keyboardlayoutservice), read-only |
| `Log` | [Log](#log), read-only |
| `LogPath` | string, read-only |
| `Loop` | [IEventLoop](#ieventloop), read-only |
| `Media` | [MediaService](winshell-media.md#mediaservice), read-only |
| `Networking` | [NetworkingService](winshell-network.md#networkingservice), read-only |
| `Notifications` | [NotificationListener](winshell-notifications.md#notificationlistener), read-only |
| `Power` | [PowerService](winshell-power.md#powerservice), read-only |
| `PowerMode` | [PowerModeService](winshell-power.md#powermodeservice), read-only |
| `Privacy` | [PrivacyService](winshell-privacy.md#privacyservice), read-only |
| `RecentDocuments` | [RecentDocuments](winshell-apps.md#recentdocuments), read-only |
| `Screens` | ObservableList of [ScreenInfo](#screeninfo), read-only |
| `Session` | [SessionService](winshell-session.md#sessionservice), read-only |
| `ShellId` | string, read-only |
| `Startup` | [StartupService](winshell-session.md#startupservice), read-only |
| `StateDir` | string, read-only |
| `Style` | [ShellStyle](#shellstyle) |
| `SystemAccent` | string, read-only |
| `SystemTray` | [SystemTray](winshell-tray.md#systemtray), read-only |
| `Version` | string, static, read-only |
| `VirtualDesktops` | [VirtualDesktopsService](winshell-windows.md#virtualdesktopsservice), read-only |
| `Wallpaper` | [WallpaperService](winshell-wallpaper.md#wallpaperservice), read-only |
| `WindowList` | [WindowListService](winshell-windows.md#windowlistservice), read-only |
| `Winland` | [WinlandService](winshell-winland.md#winlandservice), read-only |
| `StyleChanged` | Action |
| `CachePath(string path)` | string |
| `ConfigPath(string path)` | string |
| `DataPath(string path)` | string |
| `Dispose()` | void |
| `Hotkey(string sequence, Action? pressed = null)` | [Hotkey](winshell-input.md#hotkey) |
| `NewIdleMonitor()` | [IdleMonitor](winshell-power.md#idlemonitor) |
| `Open(string target)` | void |
| `Quit()` | void |
| `Reload()` | bool |
| `Run(Action of [Shell](#shell) start)` | void, static |
| `StatePath(string path)` | string |

## ShellImage

| Member | Type |
| --- | --- |
| `Load(string shellPath, int size)` | [PixelImage](#pixelimage)?, static |

## ShellIpc

| Member | Type |
| --- | --- |
| `Handle(string target, string function, Delegate handler)` | IDisposable |
| `Property<T>(string target, string property, Func of T read)` | IDisposable |
| `Signal(string target, string signal)` | [IpcSignal](#ipcsignal) |

## ShellStyle

| Member | Type |
| --- | --- |
| `ShellStyle()` | constructor |
| `Alert` | Color?, init |
| `Background` | Color?, init |
| `FontFamily` | FontFamily, init |
| `FontSize` | double, init |
| `Foreground` | Color?, init |
| `Padding` | double?, init |
| `SlotSize` | double, init |

## ShellWindow

Has the members of [ObservableObject](#observableobject).

| Member | Type |
| --- | --- |
| `Active` | bool, read-only |
| `ActualHeight` | double, read-only |
| `ActualWidth` | double, read-only |
| `Backdrop` | [WindowBackdrop](#windowbackdrop) |
| `Content` | UIElement? |
| `Corners` | [WindowCorners](#windowcorners) |
| `Height` | double |
| `InputRegion` | IReadOnlyList of Rect? |
| `Opacity` | double |
| `Origin` | Point, read-only |
| `Scale` | double, read-only |
| `Screen` | [ScreenInfo](#screeninfo)? |
| `Visible` | bool |
| `VisibleMask` | Rect? |
| `Width` | double |
| `Closed` | Action |
| `Failed` | Action of Exception |
| `Dispose()` | void |
| `Focus()` | void |
| `MapToScreen(double x, double y)` | (int, int) |
| `Of(UIElement element)` | [ShellWindow](#shellwindow)?, static |
| `Raise()` | void |
| `StartSystemMove()` | void |
| `StartSystemResize([Edges](#edges) edges)` | void |
| `Toggle()` | void |

## Surface

A WinUI Component. The members here are the ones Winshell adds.

| Member | Type |
| --- | --- |
| `Surface()` | constructor |
| `Render()` | Element |

## SurfaceProps

| Member | Type |
| --- | --- |
| `SurfaceProps(Element? Content = null, Thickness Padding = default, [ShellStyle](#shellstyle)? Style = null)` | constructor |
| `Content` | Element?, init |
| `Padding` | Thickness, init |
| `Style` | [ShellStyle](#shellstyle)?, init |

## TomlCommentAttribute

An attribute.

| Member | Type |
| --- | --- |
| `TomlCommentAttribute(string text)` | constructor |
| `Text` | string, read-only |

## UnsupportedWidgetException

An exception.

| Member | Type |
| --- | --- |
| `UnsupportedWidgetException(string message)` | constructor |

## Widget

A WinUI Component. The members here are the ones Winshell adds.

| Member | Type |
| --- | --- |
| `Orientation` | Orientation, protected, read-only |
| `Settings` | IReadOnlyDictionary of string, object, protected, read-only |
| `Shell` | [Shell](#shell), protected, read-only |
| `Style` | [ShellStyle](#shellstyle), protected, read-only |
| `Window` | [ShellWindow](#shellwindow)?, protected, read-only |
| `Setting<T>(string key, T? fallback)` | T?, protected |
| `StylePopup(UIElement content)` | FrameworkElement, protected |
| `UseNow(TimeSpan every)` | DateTime, protected |
| `UseScreen()` | [ScreenInfo](#screeninfo)?, protected |

## WidgetArea

A WinUI Component. The members here are the ones Winshell adds.

| Member | Type |
| --- | --- |
| `WidgetArea()` | constructor |
| `Render()` | Element |

## WidgetAreaProps

| Member | Type |
| --- | --- |
| `WidgetAreaProps(string Name = "", ImmutableArray of [Placement](#placement)? Defaults = null, Orientation Orientation = Orientation.Horizontal, [ShellStyle](#shellstyle)? Style = null)` | constructor |
| `Defaults` | ImmutableArray of [Placement](#placement)?, init |
| `Name` | string, init |
| `Orientation` | Orientation, init |
| `Style` | [ShellStyle](#shellstyle)?, init |

## WidgetContext

| Member | Type |
| --- | --- |
| `Orientation` | Orientation, read-only |
| `Settings` | IReadOnlyDictionary of string, object, read-only |
| `Shell` | [Shell](#shell), read-only |
| `Style` | [ShellStyle](#shellstyle), read-only |
| `Window` | [ShellWindow](#shellwindow)?, read-only |
| `StylePopup(UIElement content)` | FrameworkElement |

## Widgets

| Member | Type |
| --- | --- |
| `Register<TWidget>(string name)` | void, static |

## WindowBackdrop

| Value | Number |
| --- | --- |
| `None` | 0 |
| `Mica` | 1 |
| `Acrylic` | 2 |
| `MicaAlt` | 3 |

## WindowCorners

| Value | Number |
| --- | --- |
| `Default` | 0 |
| `Square` | 1 |
| `Round` | 2 |
| `RoundSmall` | 3 |

## WindowThumbnail

A WinUI FrameworkElement. The members here are the ones Winshell adds.

| Member | Type |
| --- | --- |
| `WindowThumbnail()` | constructor |
| `Peek` | bool |
| `Window` | [AppWindow](winshell-windows.md#appwindow)? |
| `PeekProperty` | DependencyProperty, static |
| `WindowProperty` | DependencyProperty, static |
