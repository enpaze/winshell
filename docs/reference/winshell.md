# Winshell

The configuration's program uses these types. `Shell.Run<T>()` starts one, from `Main` marked `[STAThread]`. Windows are `PanelWindow`, `PopupWindow` and `FloatingWindow`. Their content is any WinUI element. Services hang off `Shell.Current`: [apps](winshell-apps.md), [audio](winshell-audio.md), [Bluetooth](winshell-bluetooth.md), [capture](winshell-capture.md), the [Explorer taskbar](winshell-explorertaskbar.md), [media](winshell-media.md), [network](winshell-network.md), [notifications](winshell-notifications.md), [power](winshell-power.md), [session](winshell-session.md), the [tray](winshell-tray.md), [windows](winshell-windows.md) and [Winland](winshell-winland.md). `Shell.Ipc` is what `winshell ipc` calls. The user's choices are in [Preferences](#preferences).

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## Bar

A panel along one edge of every screen, with start, center and end widget areas. It is built only from Winshell's public building blocks, so a variant of your own can start as a copy of it. It shows on the next turn of the loop, once its properties are set, and Dispose removes it. A bar reserves its edge and leaves Explorer's taskbar as it is. A left or right edge runs the areas from top to bottom. One window's content is one tree: a Surface with three WidgetArea components.

Its section of the preferences file, named after Name, holds `edge`, `start`, `center` and `end`, which replace the properties of the same names while the file has them. A `[[bar.screen]]` row, as ScreenWindows matches it, may change any of them for one screen. A change to a row replaces that screen's bar, and a change to the section replaces every screen's. A value in the section that can't be used is logged, and the bar keeps the one it had. One in a row is logged under the bar's name, and that screen uses the section's.

| Member | Type | Description |
| --- | --- | --- |
| `Bar()` | constructor | A bar on the top edge with no widgets. Make it on the shell's thread. |
| `Center` | IReadOnlyList of [Placement](#placement), init | The widgets in the middle. |
| `Edge` | [Edges](#edges), init | The edge the bar runs along: one of Edges.Top, Bottom, Left and Right. |
| `End` | IReadOnlyList of [Placement](#placement), init | The widgets at the end: the right, or the bottom on a side bar. |
| `Name` | string, init | The bar's name, which names its section of the preferences file. Give each bar of a shell its own. |
| `Padding` | double?, init | The space between the areas and each end of the bar, along its length, in logical pixels: at the left and right of a top or bottom bar, and at the top and bottom of a side bar. Null takes the style's padding, or none when the style sets no padding. |
| `Reserve` | bool, init | Whether the bar reserves its edge, so maximized windows stop short of it. False lays it over them. |
| `Start` | IReadOnlyList of [Placement](#placement), init | The widgets at the start: the left, or the top on a side bar. A plain string names a widget with no settings. |
| `Style` | [ShellStyle](#shellstyle)?, init | The bar's own style, which wins over Shell.Style, or null for the shell's. |
| `Thickness` | double?, init | The height of a top or bottom bar, or the width of a side bar, in logical pixels. Null takes the style's slot size. |
| `Dispose()` | void | Removes the bar from every screen, and stops following the screens, the file and the style. |

## CaptureView

A live view of a CaptureSession. The frame is fitted and centered, keeping its aspect ratio. Its natural size is the frame's physical size divided by this window's scale. The frame is written once into the bitmap, on the thread that delivered it, so the session's loop is this window's. The view lets go of the session when it leaves the tree.

A WinUI Panel. The members here are the ones Winshell adds.

| Member | Type | Description |
| --- | --- | --- |
| `CaptureView()` | constructor | A view with no session, which shows nothing. |
| `Session` | [CaptureSession](winshell-capture.md#capturesession)? | The capture to show. Null shows nothing. |
| `SessionProperty` | DependencyProperty, static | The capture to show. Null shows nothing. |

## ColorScheme

Whether windows are light or dark. System follows Windows.

| Value | Number | Description |
| --- | --- | --- |
| `System` | 0 | Follows the light or dark mode set in Windows. |
| `Light` | 1 | Light, whatever Windows is set to. |
| `Dark` | 2 | Dark, whatever Windows is set to. |

## Edges

The edges of a screen or a window, such as the ones a panel is anchored to. These values combine.

| Value | Number | Description |
| --- | --- | --- |
| `None` | 0 | No edge. |
| `Top` | 1 | The top edge. |
| `Left` | 2 | The left edge. |
| `Right` | 4 | The right edge. |
| `Bottom` | 8 | The bottom edge. |

## Elements

Surface and WidgetArea as elements, so a component can write them into its tree. A window mounts that component with `new ReactorHostControl { ComponentFactory = () => new Dock() }`.

| Member | Type | Description |
| --- | --- | --- |
| `Surface(Element? content = null, Thickness padding = default, [ShellStyle](#shellstyle)? style = null)` | Element, static | A surface showing content in style, or the shell's style when that is null. |
| `WidgetArea(string name = "", IReadOnlyList of [Placement](#placement)? defaults = null, Orientation orientation = Orientation.Horizontal, [ShellStyle](#shellstyle)? style = null)` | Element, static | A widget area. name selects its section of the preferences file, and defaults shows until that section has a list. An empty name uses the defaults only. |

## FloatingWindow

A normal window with a title bar.

Has the members of [ShellWindow](#shellwindow).

| Member | Type | Description |
| --- | --- | --- |
| `FloatingWindow()` | constructor | A hidden window of the running shell, 640 by 480 unless `Width` and `Height` say otherwise. |
| `Fullscreen` | bool | Whether the window is borderless and covering its monitor. A minimized fullscreen window stays fullscreen. |
| `Maximized` | bool | Whether the window is maximized. It follows the user's own maximize and restore. Setting it true leaves Fullscreen. |
| `MaximumSize` | Size | The largest size the user can drag the window to, in logical pixels. A width or height of 0 sets no limit. |
| `Minimized` | bool | Whether the window is minimized. It follows the user's own minimize and restore. |
| `MinimumSize` | Size | The smallest size the user can drag the window to, in logical pixels. A width or height of 0 sets no limit. |
| `Owner` | [FloatingWindow](#floatingwindow)? | The window this one stays above and closes with. It can be set before that window exists, or changed later. A window can't own itself or a window that owns it. |
| `Title` | string | The text in the title bar and on the taskbar. |

## IEventLoop

The thread the shell's objects live on, and their clock. Work from other threads is posted to it.

| Member | Type | Description |
| --- | --- | --- |
| `Time` | TimeProvider, read-only | The clock the shell's objects read. A timer it creates runs on another thread: use Schedule for work on the loop. |
| `Post(Action action)` | void | Runs the action on the loop's thread. Safe to call from any thread. |
| `Schedule(TimeSpan delay, Action action)` | IDisposable | Runs the action once after the delay, on the loop's thread. Dispose to cancel. |

## IpcException

A refused IPC request. A handler throws it to refuse a call: its message goes to the client as it is, where another exception arrives with its type name.

An exception.

| Member | Type | Description |
| --- | --- | --- |
| `IpcException(string message)` | constructor |  |

## IpcSignal

A signal of a ShellIpc target. Use it on the shell's thread.

| Member | Type | Description |
| --- | --- | --- |
| `Name` | string, read-only | The signal's name, which `winshell ipc wait` and `listen` take. |
| `Emit(string value = "")` | void | Tells every `winshell ipc wait` and `listen` following this signal, with value. |

## Log

Writes messages as ` WARN category: message`.

| Member | Type | Description |
| --- | --- | --- |
| `Default` | [Log](#log), static | This process's log. In a running shell it writes to the instance's log, which `winshell log` shows. |
| `Debug(string category, string message)` | void | Writes a LogLevel.Debug message, kept only when the shell runs with `--verbose`. |
| `Error(string category, string message)` | void | Writes a LogLevel.Error message. |
| `Info(string category, string message)` | void | Writes a LogLevel.Info message. |
| `Warn(string category, string message)` | void | Writes a LogLevel.Warn message. |
| `Write([LogLevel](#loglevel) level, string category, string message)` | void | Writes message under category, such as `tray`. A LogLevel.Debug message is dropped unless the shell runs with `--verbose`. |

## LogLevel

How serious a log message is, from least to most.

| Value | Number | Description |
| --- | --- | --- |
| `Debug` | 0 | Detail for tracking down a problem. Kept only when the shell runs with `--verbose`. |
| `Info` | 1 | Normal progress worth recording. |
| `Warn` | 2 | Something went wrong and the shell carried on. |
| `Error` | 3 | An operation failed. |
| `Fatal` | 4 | A failure that ends the process. |

## ObservableList

A list a service keeps current. A configuration reads it, and only its service changes it. A change is raised on the thread that made it, which for a service is its event loop, so Reactor can show it. An item that raises INotifyPropertyChanged.PropertyChanged raises this list too, until the item is removed, replaced or cleared.

| Member | Type | Description |
| --- | --- | --- |
| `Count` | int, read-only | How many items there are. |
| `Item` | T?, read-only | The item at index. |
| `CollectionChanged` | NotifyCollectionChangedEventHandler | Raised when an item is added, removed, moved or replaced. |
| `PropertyChanged` | PropertyChangedEventHandler | Raised when ObservableList.Count or the items change, and when an item raises INotifyPropertyChanged.PropertyChanged, for Reactor. |
| `Contains(T? item)` | bool | Whether item is in the list. |
| `GetEnumerator()` | IEnumerator of T | Goes through the items in order. |
| `IndexOf(T? item)` | int | Where item is, or -1 when it is not in the list. |

## ObservableObject

The base of what a service exposes. A change is raised on the thread that made it, which for a service is its event loop, so Reactor can read it. A list property, once read through Read, raises this object when that list changes, so a change inside the list reaches the object.

| Member | Type | Description |
| --- | --- | --- |
| `PropertyChanged` | PropertyChangedEventHandler | Raised when a property's value changes, with the property's name. |

## PanelWindow

A window anchored to an edge of a screen. It can reserve that edge.

Has the members of [ShellWindow](#shellwindow).

| Member | Type | Description |
| --- | --- | --- |
| `PanelWindow()` | constructor | A hidden panel of the running shell, anchored to no edge until Anchors is set. |
| `AboveWindows` | bool | Stays above normal windows. False puts it below them, as a wallpaper is. |
| `Anchors` | [Edges](#edges) | The edges the panel sticks to. Two opposite edges stretch it across the screen. |
| `CoversScreen` | bool | Lays the panel out on the whole screen, over the taskbar and other bars, and reserves nothing. False keeps it in the work area. |
| `Focusable` | bool | Whether the panel can take the keyboard. A panel that cannot never takes activation from the app in front. |
| `Margins` | Thickness | The gap, in logical pixels, between the panel and each edge it is anchored to. Other edges' margins are unused. |
| `ReservedSize` | double | How much of its edge the panel reserves, in logical pixels, so maximized windows stay out of it. |

## PixelImage

A picture in memory: BGRA, one byte a channel, top row first, with no gap between rows. Nothing writes to the pixels after this is made, so the constructor keeps `bgra` rather than copying it, and a UI can hold on to one.

| Member | Type | Description |
| --- | --- | --- |
| `PixelImage(int width, int height, byte[] bgra, bool premultiplied = false)` | constructor | A picture of the given size that keeps bgra as its pixels. |
| `Bgra` | ReadOnlySpan of byte, read-only | The pixels: BGRA, four bytes a pixel, top row first. Read-only. |
| `Height` | int, read-only | The height in pixels. |
| `Premultiplied` | bool, read-only | Whether the color channels are already multiplied by alpha. |
| `Width` | int, read-only | The width in pixels. |
| `EncodePng()` | byte[] | The picture as a PNG file. Blocks while Windows encodes it. |

## PixelImageExtensions

Pictures in memory as sources WinUI can show.

| Member | Type | Description |
| --- | --- | --- |
| `ToImageSource([PixelImage](#pixelimage) image)` | ImageSource, static | Makes a new source for a WinUI `Image`, with the picture's pixel size and colors. Call on the UI thread. The pixels are copied and premultiplied when needed; the picture is unchanged. A source that keeps its own copy of the pixels. |

## Placement

One entry in a widget area. Several placements may name the same widget. A string converts to a placement with no settings, so a list of placements can be written `["clock", "battery"]`.

| Member | Type | Description |
| --- | --- | --- |
| `Placement(string Widget, IReadOnlyDictionary of string, object? Settings = null)` | constructor |  |
| `Settings` | IReadOnlyDictionary of string, object?, init | This placement's settings, or null for no settings. Widgets should treat nested values as read-only too. |
| `Widget` | string, init | The case-sensitive name registered with Widgets.Register. |
| `Check(object value)` | string, static | Why value is not a list of placement tables, or `""` when each table names a widget. A placement table is `{ widget, ...settings }`: `widget` is the widget's name, and every other key is one setting. A list of tables that misses a name is `must name a widget in each of its tables`. Any other value is `must be a list of tables that each name a widget`. |
| `Format(IReadOnlyList of [Placement](#placement) placements)` | IReadOnlyList of IReadOnlyDictionary of string, object, static | The tables a preferences file writes for placements. Each is `{ widget, ...settings }`, with `widget` first. A setting named `widget` is left out, so the placement's own name is the one written. |
| `Parse(object tables)` | IReadOnlyList of [Placement](#placement), static | The placements in a list of tables Check accepted, in that order. `widget` names each one and the other keys are its settings. A table with no other keys still has empty settings, which is not the null of a placement written with no settings in code. Throws ArgumentException when the value is not a list of tables that each name a widget. |

## PopupWindow

A frameless window placed next to another window.

Has the members of [ShellWindow](#shellwindow).

| Member | Type | Description |
| --- | --- | --- |
| `PopupWindow()` | constructor | A hidden popup of the running shell, 100 by 100 at the virtual desktop's origin until Rect is set. |
| `GrabFocus` | bool | Takes the keyboard while shown. Focus does nothing unless this is set. A click outside the popup and its open flyouts, or Esc, then dismisses it. |
| `Rect` | Rect | Where the popup sits, in logical pixels on the virtual desktop. |
| `Dismissed` | Action | A click outside the popup and its open flyouts, or Esc, dismissed it. It is hidden. |

## Preferences

One section of the shell's preferences file, `preferences\<shell id>.toml` in the config folder, as an object. A component's section is named after the component, as in `[bar]`. A type made with no name holds the configuration's own preferences, the keys before the first section. A type derives from this and has one public read-write property per key, the key being the property's name with a lowercase first letter. Its type is string, bool, int, long, double, IReadOnlyDictionary of string and object for a table, IReadOnlyList of any of those for an array or an array of tables, or object for anything. A property that holds null until the file has the key is nullable. A default belongs in the property's initializer. The section is read in the constructor, and after that the object follows the file: a save changes only the properties whose keys changed. A key the file does not have holds its default. Assigning a property writes it into the section, on the shell's thread, and leaves the rest of the file as it is. When the file is missing, it is written with each section's keys at their defaults, commented out, under their TomlCommentAttribute, and a component's section the file does not have is added at its end the same way. Several objects may read one section. Make one on the shell's thread and dispose it when its component goes.

Has the members of [ObservableObject](#observableobject).

| Member | Type | Description |
| --- | --- | --- |
| `Dispose()` | void | Stops following the file. A write of this section that was on its way runs now. |

## ScreenInfo

A monitor, as the platform reports it. Coordinates are logical pixels on the virtual desktop: a monitor keeps its physical top-left corner and its size is divided by its scale, so monitors of mixed DPI never overlap.

| Member | Type | Description |
| --- | --- | --- |
| `ScreenInfo(string Id, string Name, string Model, string Manufacturer, string SerialNumber, Rect Geometry, Rect WorkArea, double DevicePixelRatio, double PhysicalWidthMm, double PhysicalHeightMm, int RefreshRate, bool IsPrimary)` | constructor |  |
| `DevicePixelRatio` | double, init | The monitor's scale: physical pixels per logical pixel, such as 1.5 at 150%. |
| `Geometry` | Rect, init | The whole monitor. |
| `Id` | string, init | The Windows display device name, such as `\\.\DISPLAY1`. |
| `IsPrimary` | bool, init | Whether this is the primary monitor, which Windows puts at the origin. |
| `Manufacturer` | string, init | The three-letter PnP code of the monitor's maker, such as `GSM`. Can be empty. |
| `Model` | string, init | The monitor's name as Windows lists it, such as `Generic PnP Monitor`. Can be empty. |
| `Name` | string, init | The display device name without its `\\.\` prefix, such as `DISPLAY1`. |
| `PhysicalHeightMm` | double, init | The height of the picture, in millimeters, as the monitor reports it. 0 when unknown. |
| `PhysicalWidthMm` | double, init | The width of the picture, in millimeters, as the monitor reports it. 0 when unknown. |
| `RefreshRate` | int, init | The refresh rate in hertz, or 60 when Windows does not say. |
| `SerialNumber` | string, init | The instance part of the monitor's Windows device id, which tells two monitors of one model apart. Can be empty. |
| `WorkArea` | Rect, init | The monitor without the taskbar and the edges panels reserve. |

## ScreenWindows

One window on each screen for a component, such as a bar or a dock. It makes a screen's window when the screen appears and disposes it when the screen goes, and leaves every other screen's window as it is, so their widgets keep their state. A screen whose size, scale or work area changes keeps its window, and the screen it was made with: read Shell.Screens for the screen as it is now. A screen that differs from the rest has a row in the component's section of the preferences file, a `[[dock.screen]]` table with its `manufacturer`, `model` and `serial` as strings. A row matches a screen when all three equal the screen's ScreenInfo.Manufacturer, ScreenInfo.Model and ScreenInfo.SerialNumber, so an empty one is written `""`, and the first match wins. The name Windows numbers, such as `DISPLAY1`, is never matched: Windows renumbers it. A row for a screen that is not connected stays in the file and matches no other screen. When a screen's row changes, its window is disposed and made again from the new row, on the next turn of the loop, once the whole save is read. The log says, under the component's name, which screens have a row of their own.

It is a shortcut, not a limit: a configuration can still make any window on any screen itself. Make one on the shell's thread and dispose it when the component goes, which disposes its windows.

| Member | Type | Description |
| --- | --- | --- |
| `ScreenWindows(string name, Func of [ScreenInfo](#screeninfo), IReadOnlyDictionary of string, object, IDisposable make)` | constructor | Makes the window of every screen there is now, and follows the screens and the rows from then on. |
| `Dispose()` | void | Disposes every window and stops following the screens and the rows. All are attempted even if one disposal throws. |

## Shell

The running shell: its screens and services. There is one in a process. Each service is made the first time it is read, so a service a configuration does not use costs nothing and asks Windows for nothing.

| Member | Type | Description |
| --- | --- | --- |
| `AccentColor` | string | The accent every window follows, as `#RRGGBB`, or `""` to follow Windows. |
| `Apps` | [AppsService](winshell-apps.md#appsservice), read-only | The apps Start lists. |
| `Audio` | [AudioService](winshell-audio.md#audioservice), read-only | Playback and recording devices, and the audio sessions on them. |
| `Bluetooth` | [BluetoothService](winshell-bluetooth.md#bluetoothservice), read-only | The Bluetooth adapter and the paired and nearby devices. |
| `Brightness` | [BrightnessService](winshell-power.md#brightnessservice), read-only | The brightness of the connected monitors that report one. |
| `CacheDir` | string, read-only | This shell's cache folder, which exists once read. |
| `ColorScheme` | [ColorScheme](#colorscheme) | The color scheme every window follows. |
| `ConfigDir` | string, read-only | The folder of this configuration's program, where its own files are. |
| `ConfigRoot` | string, read-only | The config folder: `%APPDATA%\winshell`, or `WINSHELL_CONFIG_DIR` when it is set. |
| `Cpu` | [CpuService](winshell-cpu.md#cpuservice), read-only | How busy the processors are, measured only while something listens. |
| `Current` | [Shell](#shell), static, read-only | The shell of this process. Reading it before Run has started it throws. |
| `DataDir` | string, read-only | This shell's data folder, which exists once read. |
| `ExplorerTaskbar` | [ExplorerTaskbarService](winshell-explorertaskbar.md#explorertaskbarservice), read-only | Whether Explorer's taskbar is hidden. Winshell shows it again when it exits. |
| `Ipc` | [ShellIpc](#shellipc), read-only | What `winshell ipc` reaches in this shell. |
| `IsDark` | bool, read-only | Whether windows are dark. ColorScheme overrides Windows mode, and ColorScheme.System follows it. |
| `JumpLists` | [JumpListService](winshell-apps.md#jumplistservice), read-only | The recent and frequent documents of an app, as its jump list shows them. |
| `KeyboardLayouts` | [KeyboardLayoutService](winshell-keyboard.md#keyboardlayoutservice), read-only | The installed keyboard layouts, and the one the foreground window is using. Read only while something listens. |
| `Log` | [Log](#log), read-only | This instance's log, which `winshell log` shows. |
| `LogPath` | string, read-only | This instance's log file, or empty when it writes none. |
| `Loop` | [IEventLoop](#ieventloop), read-only | The shell's thread. Post work from other threads to it before touching windows or services. |
| `Media` | [MediaService](winshell-media.md#mediaservice), read-only | The media sessions Windows lets the shell see and control. |
| `Networking` | [NetworkingService](winshell-network.md#networkingservice), read-only | The network adapters, connection profiles, radios and connectivity. |
| `Notifications` | [NotificationListener](winshell-notifications.md#notificationlistener), read-only | Windows Notification Center. Reading this never prompts for access. |
| `Options` | [ShellOptions](#shelloptions), read-only | The options the configuration passed to Run. |
| `Power` | [PowerService](winshell-power.md#powerservice), read-only | The batteries of this PC. |
| `PowerMode` | [PowerModeService](winshell-power.md#powermodeservice), read-only | The Windows power mode: best power efficiency, balanced, or best performance. |
| `Privacy` | [PrivacyService](winshell-privacy.md#privacyservice), read-only | Which apps are using the microphone, the camera or location. Read only while something listens. |
| `RecentDocuments` | [RecentDocuments](winshell-apps.md#recentdocuments), read-only | The documents in the user's Recent folder, found by name. |
| `Screens` | ObservableList of [ScreenInfo](#screeninfo), read-only | The connected screens. The primary screen comes first. |
| `Session` | [SessionService](winshell-session.md#sessionservice), read-only | The Windows session: locking it, and what state it is in. |
| `ShellId` | string, read-only | The name that identifies this configuration across runs. |
| `Startup` | [StartupService](winshell-session.md#startupservice), read-only | Whether this shell runs when the user signs in. |
| `StateDir` | string, read-only | This shell's state folder, which exists once read. |
| `Style` | [ShellStyle](#shellstyle)? | The style of every widget area, Surface, styled popup and tooltip, or null for the built-in style, which follows Windows' colors, Segoe UI and Segoe Fluent Icons. Set it on the shell's thread. A new value reaches what is already on screen, and the next tooltip that opens. A bar's, an area's or a surface's own style wins over it, except for tooltips. |
| `SystemAccent` | string, read-only | The accent Windows is using, as `#RRGGBB`. |
| `SystemTray` | [SystemTray](winshell-tray.md#systemtray), read-only | The notification area. Only one running instance in the Windows session owns it. |
| `Version` | string, static, read-only | The Winshell release this program is built on. |
| `VirtualDesktops` | [VirtualDesktopsService](winshell-windows.md#virtualdesktopsservice), read-only | The virtual desktops Task View shows. |
| `Wallpaper` | [WallpaperService](winshell-wallpaper.md#wallpaperservice), read-only | The desktop wallpaper: each screen's image, the fit and the background color. |
| `WindowList` | [WindowListService](winshell-windows.md#windowlistservice), read-only | The app windows the taskbar and Alt+Tab list. |
| `Winland` | [WinlandService](winshell-winland.md#winlandservice), read-only | The Winland window manager's Monitors, Workspaces and windows. The lists are empty while it is not running. |
| `StyleChanged` | Action | Raised on the shell's thread after Style is set, so what reads the style outside a widget area, such as the thickness of a Bar, can read it again. |
| `CachePath(string path)` | string | A path in CacheDir. |
| `ConfigPath(string path)` | string | A path in ConfigDir. |
| `DataPath(string path)` | string | A path in DataDir. |
| `Dispose()` | void | Closes Winshell's notices, stops the services, and closes every window. One that throws doesn't stop the rest: its exception is thrown once all have stopped. Quit does this and also ends the shell. |
| `NewHotkey()` | [Hotkey](winshell-input.md#hotkey) | A hotkey that works anywhere in Windows. Set its Hotkey.Sequence, and dispose it to give the keys back. |
| `NewIdleMonitor()` | [IdleMonitor](winshell-power.md#idlemonitor) | Tells when the person has not used the PC for a while. Input this process sent does not count. |
| `Open(string target)` | void | Opens a Settings page, an app or a URL as Windows would, such as `ms-settings:sound`, `https://example.com` or an app's path: a URI or a path, not a command line. It opens off the loop, so a slow app does not hold up the shell. What Windows can't start, such as a path that is not there, is logged, not thrown. A scheme no app handles shows Windows' own app picker. |
| `Quit()` | void | Ends the shell. Its windows close and Run returns. Something that fails to stop is logged and reported, and the shell ends anyway. |
| `Reload()` | bool | Replaces this instance with a new process of the same program, which takes over once this one has gone. False when that process did not start: the shell then keeps running. |
| `Run<T>([ShellOptions](#shelloptions)? options = null)` | void, static | Runs a configuration until the shell quits, on this thread, as a Winshell instance: `winshell list`, `log`, `kill`, `reload` and `ipc` reach it. Call it from `Main`, marked `[STAThread]`. The program takes `--no-duplicate` and `-d` on its command line. `--no-duplicate` exits at once when its shell id is already running. `-d` starts this program again and returns, so the caller does not wait, and that start keeps `--after`. T is a ShellConfiguration with a public parameterless constructor. |
| `StatePath(string path)` | string | A path in StateDir. |

## ShellConfiguration

A configuration: the program that defines a shell. Start makes its windows.

| Member | Type | Description |
| --- | --- | --- |
| `Start([Shell](#shell) shell)` | void | Runs once, on the shell's thread, after WinUI has started. |

## ShellImage

The icons the Windows shell shows for programs, files and packaged apps.

| Member | Type | Description |
| --- | --- | --- |
| `Load(string shellPath, int size)` | [PixelImage](#pixelimage)?, static | Reads the icon the Windows shell shows for an item. Blocks while the shell reads it. The icon, premultiplied, or null when the shell has none for the item. |

## ShellIpc

What `winshell ipc` reaches in a C# configuration: functions, properties and signals, by target. A function's own types declare what it takes and returns, and only string, int, bool, double, float and Color cross the pipe. The CLI's text is parsed into those types and nothing is ever evaluated. Use this on the shell's thread.

| Member | Type | Description |
| --- | --- | --- |
| `Handle(string target, string function, Delegate handler)` | IDisposable | Lets `winshell ipc call` run handler, such as `(string page) => Open(page)`. Dispose the result to take it away. A later handler for the same function replaces this one. Throws ArgumentException when the handler takes or returns a type IPC can't pass. |
| `Property<T>(string target, string property, Func of T read)` | IDisposable | Lets `winshell ipc prop get` read a value. Dispose the result to take it away. Throws ArgumentException when T is a type IPC can't pass. |
| `Signal(string target, string signal)` | [IpcSignal](#ipcsignal) | A signal `winshell ipc wait` and `listen` can follow. |

## ShellOptions

What a configuration sets before its shell starts.

| Member | Type | Description |
| --- | --- | --- |
| `ShellOptions()` | constructor |  |
| `CacheDir` | string?, init | Another folder for Shell.CacheDir, read as DataDir is. |
| `DataDir` | string?, init | Another folder for Shell.DataDir. A relative path is in the configuration's project folder, so a rebuild of `bin\shell` leaves it, and a program a reload built in `bin\shell.next` uses that same folder. A program in neither resolves a relative path next to itself. `%NAME%` expands, and `$BASE` is the folder above `by-shell`, such as `%LOCALAPPDATA%\winshell\data`. |
| `ShellId` | string?, init | The name that identifies this configuration across runs. It names its data, state and cache folders. Without it, a program in the config folder is named after its folder, and one anywhere else after its path. |
| `StateDir` | string?, init | Another folder for Shell.StateDir, read as DataDir is. |

## ShellStyle

The look of a shell, a bar or a widget area: background, foreground, alert color, font, slot size and padding. Winshell writes it into the resources plain controls read. Null, on the shell, is the built-in style, which follows Windows' colors in light, in dark and in a contrast theme.

| Member | Type | Description |
| --- | --- | --- |
| `ShellStyle()` | constructor |  |
| `Alert` | Color, init | Something that needs attention, such as a network with no internet. A widget reads it from WidgetContext.Style. |
| `Background` | Color, init | Behind a surface, such as a bar, and a popup. |
| `FontFamily` | FontFamily, init | Text. The built-in style uses Segoe UI Variable, then Segoe UI. |
| `FontSize` | double, init | Text, in logical pixels. The built-in style uses 14. |
| `Foreground` | Color, init | Text, and a glyph that takes its color from the control that holds it. |
| `Padding` | double?, init | The space at each end of a bar, in logical pixels. Null gives a bar none. The built-in style uses 4. |
| `SlotSize` | double, init | One button, and the thickness of a bar, in logical pixels. The built-in style uses 32. |

## ShellWindow

A top-level surface of the shell. Set Content to any UIElement: XAML, or a `ReactorHostControl`. Its background stays transparent, so Backdrop shows through.

Has the members of [ObservableObject](#observableobject).

| Member | Type | Description |
| --- | --- | --- |
| `Active` | bool, read-only | Whether this window is the foreground window. |
| `ActualHeight` | double, read-only | The height the window has now, in logical pixels. 0 before it is first shown. |
| `ActualWidth` | double, read-only | The width the window has now, in logical pixels. 0 before it is first shown. |
| `Backdrop` | [WindowBackdrop](#windowbackdrop) | The Windows 11 material behind the content. WindowBackdrop.None by default. |
| `Content` | UIElement? | What the window shows: any UIElement, or null for nothing. |
| `Corners` | [WindowCorners](#windowcorners) | How the window's corners are drawn. WindowCorners.Default by default. |
| `Height` | double | The height in logical pixels. NaN leaves it to the window's kind: anchors, or the default size. |
| `InputRegion` | IReadOnlyList of Rect? | The parts of the window that take clicks, in logical pixels; clicks elsewhere go to the window below. Null is the whole window. An empty list lets every click through. |
| `Opacity` | double | Fades the content. The material behind it does not fade. |
| `Origin` | Point, read-only | The window's origin on the virtual desktop, in logical pixels. |
| `Scale` | double, read-only | How many physical pixels this window draws for one logical pixel. |
| `Screen` | [ScreenInfo](#screeninfo)? | The screen this window is on, or was asked to be on. Null is the primary screen. |
| `Visible` | bool | Whether the window is shown. A new window is hidden until this is set. It turns false when the user closes the window. |
| `VisibleMask` | Rect? | Clips the content to a rectangle, in logical pixels. Null shows all of it. |
| `Width` | double | The width in logical pixels. NaN leaves it to the window's kind: anchors, or the default size. |
| `Closed` | Action | The user closed the window. It is hidden, and can be shown again. |
| `Failed` | Action of Exception | The native window failed. It was rebuilt with the same content. |
| `Dispose()` | void | Closes the window for good and releases its native window. It can't be shown again. |
| `Focus()` | void | Takes the keyboard, if this kind of window may. A panel takes it only while focusable, and a popup only while it grabs focus. |
| `MapToScreen(double x, double y)` | (int, int) | A point in logical window coordinates, in physical pixels on the virtual desktop. |
| `Of(UIElement element)` | [ShellWindow](#shellwindow)?, static | The shell window element is drawn in, or null when it is not in one yet. A widget is created before its area is in a window, so one that depends on the screen calls this from its loaded event. The window's root holds the window. A table keyed on the `XamlRoot` would not: the wrapper of a `XamlRoot` goes in a collection, and the next read of it is another object. That window, or null when element is not drawn in one. |
| `Raise()` | void | Puts the window above the others at its level, without focusing it. |
| `StartSystemMove()` | void | Lets the user move the window by dragging, as a title bar does. Call it when a pointer button goes down. |
| `StartSystemResize([Edges](#edges) edges)` | void | Lets the user resize the window by dragging an edge or a corner, as its border does. Call it when a pointer button goes down. Two adjacent edges, such as top and left, name a corner. Any other combination resizes from the bottom-right corner. |

## Surface

Paints the style's `ShellBackgroundBrush` behind its content in the window's theme, as plain controls take theirs, so a switch between light and dark while the shell runs reaches it. A bar's window shows one of these as its tree, with the bar's areas inside it.

A WinUI Component`1. The members here are the ones Winshell adds.

| Member | Type | Description |
| --- | --- | --- |
| `Surface()` | constructor |  |
| `Render()` | Element | The background under the content. The content is not keyed, so a new style leaves it loaded. |

## SurfaceProps

What a Surface shows. Content is one element, often a WidgetArea, and it stays loaded when the style changes. A null Style follows Shell.Style.

| Member | Type | Description |
| --- | --- | --- |
| `SurfaceProps(Element? Content = null, Thickness Padding = default, [ShellStyle](#shellstyle)? Style = null)` | constructor |  |
| `Content` | Element?, init | What the surface holds, or null for nothing. |
| `Padding` | Thickness, init | The space around Content, in logical pixels. |
| `Style` | [ShellStyle](#shellstyle)?, init | The surface's own style, which wins over the shell's, or null for the shell's. |

## TomlCommentAttribute

The comment written above a key the file does not have yet. A line break starts another comment line.

An attribute.

| Member | Type | Description |
| --- | --- | --- |
| `TomlCommentAttribute(string text)` | constructor |  |
| `Text` | string, read-only | The comment's text, without the leading `#`. |

## UnsupportedWidgetException

A widget throws this with the reason it cannot run in this placement.

An exception.

| Member | Type | Description |
| --- | --- | --- |
| `UnsupportedWidgetException(string message)` | constructor |  |

## WidgetArea

An ordered area of widgets, in one direction. A bar shows three of these in one tree. The shell's Shell.Style, or the area's own, styles the plain controls a widget renders; area-sensitive XAML uses ThemeResource. Unloading the host leaves the widgets mounted; disposing the host unmounts them.

A WinUI Component`1. The members here are the ones Winshell adds.

| Member | Type | Description |
| --- | --- | --- |
| `WidgetArea()` | constructor |  |
| `Render()` | Element | The placements, in order. A widget whose name stays is the same component. |

## WidgetAreaProps

What a WidgetArea shows. Name selects the area's section of the preferences file. That section holds one `widgets` list, which replaces Defaults while the file has it. An empty name uses only the defaults. The component reads the section when it renders and follows later saves.

| Member | Type | Description |
| --- | --- | --- |
| `WidgetAreaProps(string Name = "", IReadOnlyList of [Placement](#placement)? Defaults = null, Orientation Orientation = Orientation.Horizontal, [ShellStyle](#shellstyle)? Style = null)` | constructor |  |
| `Defaults` | IReadOnlyList of [Placement](#placement)?, init | The author's ordered placements. A missing list is none. |
| `Name` | string, init | The section, or `""` to use only Defaults. |
| `Orientation` | Orientation, init | The direction the placements run. |
| `Style` | [ShellStyle](#shellstyle)?, init | This area's own style, which wins over the shell's, or null for the shell's. |

## WidgetContext

The props of one placement: the shell, the shell window the area is drawn in, the placement's settings and the area's orientation, plus the styling of a popup the widget opens. A widget reads them as `Props`. A changed placement, or the area moving into a window, is a new value of this record, and the component renders again.

| Member | Type | Description |
| --- | --- | --- |
| `Orientation` | Orientation, read-only | The direction in which the area arranges its placements. |
| `Settings` | IReadOnlyDictionary of string, object, read-only | This placement's settings. Empty when the placement supplied none. |
| `Shell` | [Shell](#shell), read-only | The shell that owns the area. |
| `Style` | [ShellStyle](#shellstyle)?, read-only | The style this placement shows: the area's own, or the shell's, or null for the built-in style. A widget reads ShellStyle.Alert from it when something needs attention. |
| `Window` | [ShellWindow](#shellwindow)?, read-only | The shell window this area is drawn in, and so the screen that window is on, or null before the area is in a window. The area renders the widget again once it is. |
| `StylePopup(UIElement content)` | FrameworkElement | Wraps content for a PopupWindow or a flyout in the area's styling. Assign the result to the popup's Content on the shell's thread. A widget may call it as soon as it has its context, before the area is in a host. Each call loads a fresh dictionary of the area's style, preserving the content's own resources, so the popup's plain controls and a component hosted in it look like the area's. The wrapper follows later changes to that style at once, until the area removes the widget or is disposed. The widget still owns and disposes its popup windows. A new wrapper to use as the popup window's content. |

## Widgets

The widget components available to this configuration, registered before creating its areas.

| Member | Type | Description |
| --- | --- | --- |
| `Register<TWidget>(string name)` | void, static | Registers a component widget under a case-sensitive name for the life of the program. A duplicate name throws. One instance is made for each placement, on the shell's thread, and a changed placement renders that instance again with the new WidgetContext. A component that throws while rendering shows its error in the area, and the area logs it. |

## WindowBackdrop

What a window shows behind its content: nothing, or a Windows 11 material.

| Value | Number | Description |
| --- | --- | --- |
| `None` | 0 | No material: what the content does not cover is transparent. |
| `Mica` | 1 | Mica, tinted by the desktop background, as Windows Settings uses. |
| `Acrylic` | 2 | Acrylic, a blur of what is behind the window, as Windows' flyouts use. |
| `MicaAlt` | 3 | Mica Alt, a stronger tint of Mica, as tabbed title bars use. |

## WindowCorners

How a window's corners are drawn.

| Value | Number | Description |
| --- | --- | --- |
| `Default` | 0 | The kind's own: a floating window's are Windows' choice, a popup with a backdrop is rounded, and the rest are square. |
| `Square` | 1 | Square corners. |
| `Round` | 2 | Rounded corners. |
| `RoundSmall` | 3 | Corners with a smaller rounding. |

## WindowThumbnail

A live preview of an app window, drawn by DWM into the shell window that holds this element. The image is in physical pixels relative to that window, and it is painted over the XAML, so nothing can overlap it. It follows this element's layout and the shell window moving, and it lets go of the preview when it leaves the tree.

A WinUI FrameworkElement. The members here are the ones Winshell adds.

| Member | Type | Description |
| --- | --- | --- |
| `WindowThumbnail()` | constructor | A thumbnail with no window, which shows nothing. It takes no clicks, and screen readers skip it. |
| `Peek` | bool | While true, Windows shows Window at full size and hides the others. It stays off when the user turned peek off, and it ends when this element unloads or the window goes. |
| `Window` | [AppWindow](winshell-windows.md#appwindow)? | The app window to preview, from WindowListService.Windows. Null shows nothing. |
| `PeekProperty` | DependencyProperty, static | While true, Windows shows Window at full size and hides the others. It stays off when the user turned peek off, and it ends when this element unloads or the window goes. |
| `WindowProperty` | DependencyProperty, static | The app window to preview, from WindowListService.Windows. Null shows nothing. |
