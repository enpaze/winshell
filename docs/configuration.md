# Configuration guide

A configuration is a .NET project that references the Winshell package and builds into one program: your shell. This page begins with the starter, a bar with a clock and a battery, and reads it file by file. The sections after that explain each part in full.

- The [reference](reference/index.md) lists every type and member.
- The [Omarchy-style example](https://github.com/enpaze/winshell/tree/main/examples/omarchy-style) is a whole panel to copy from.
- The C# on this page is copied from files that build: the starter and [GuideSamples.cs](https://github.com/enpaze/winshell/blob/main/tests/Winshell.Shell.Tests/GuideSamples.cs). Nothing in GuideSamples runs.

## Your first bar

You need the [.NET SDK](https://dotnet.microsoft.com/download). Make the starter and run it:

```powershell
winshell new starter
winshell run -c starter
```

A bar shows along the top of every screen, with the time in the middle and the battery's charge at the end. It reserves that edge, so maximized windows stop short of it. `winshell kill` closes it. The project is in `%APPDATA%\winshell\starter`, and the C# below is its own text.

### The shell

`Program.cs` starts the shell and says what it shows. The function passed to `Shell.Run` runs once the shell is up, and makes one [bar](#a-bar-on-every-screen):

```csharp
Shell.Run(shell =>
{
    _ = new Bar { Edge = Edges.Top, Center = ["clock"], End = ["battery"] };
});
```

The bar keeps itself on screen, so nothing needs to hold it. `Edges.Bottom` puts it at the bottom, and the bar's `Start` is a third list, at the left. With no [style](#style) set, it takes Windows' colors and font, in light or dark.

### A clock

A widget is a public class that derives from `Widget`. The shell finds it when it starts and names it after its class, so `Clock` is the widget `clock`, as [Widgets](#widgets) describes. `Render` returns the controls it shows. The clock renders one text block. The time is not a service, so `UseNow` asks it to render again each second and returns the time:

```csharp
public sealed class Clock : Widget
{
    public override Element Render()
    {
        var time = UseNow(TimeSpan.FromSeconds(1));
        return TextBlock($"{time:t}").AutomationName("Clock");
    }
}
```

`{time:t}` is the time as Windows is set to write it, such as `14:05` or `2:05 PM`. `UseNow` stops when the clock leaves the tree. `AutomationName` is the name `winshell inspect` and Narrator use.

### A battery

The battery observes the shell's `Power` service, which holds what Windows reports:

```csharp
public sealed class Battery : Widget
{
    public override Element Render()
    {
        var battery = UseObservable(Shell.Power.DisplayDevice);
        return TextBlock($"{battery.Percentage:P0}")
            .Margin(8, 0, 8, 0)
            .IsVisible(battery.IsPresent)
            .AutomationName("Battery");
    }
}
```

`Shell` is the running shell. [UseObservable](#keep-a-component-current) renders the component again when that device changes. A PC with no battery has `IsPresent` false, so the text collapses. The margin keeps it off the screen's edge. A plain text block takes the [style](#style), so the battery names no brush of its own.

`HealthSupported` is true when Windows reported the capacities behind `HealthPercentage`, including when that percentage is 0. A shell that shows health reads the flag first.

`shell.PowerMode.Mode` and `Effective` are `Unknown` when Windows did not report a power mode. Setting `Mode` to `Unknown` does nothing: that is not a position of the slider.

### Widget names

The bar places `clock` and `battery`, the names of the classes `Clock` and `Battery`. `Program.cs` names no widget class: the shell finds each one when it starts, in your project and in the projects and packages it references, as [Widgets](#widgets) describes.

The files have no `using` line for Winshell or Reactor. The package adds them, as [The package's usings](#the-packages-usings) says.

### Change it

Close the bar with `winshell kill`, and run the starter under `winshell dev` instead. It applies each edit to the running bar as you save it, until you press Ctrl+C:

```powershell
winshell kill
winshell dev -c starter
```

Change `{time:t}` in `Clock.cs` to `{time:T}` and save it, and the clock shows the seconds. The bar stays where it is, and so does the battery.

`Start = ["battery"]` in place of `End` moves the battery to the left. Or write a widget of your own: a class like these, placed by its name. [Widgets](#widgets) has the rest, and [A service](#a-service) lists what else the shell can read. Whoever uses the shell can rearrange the bar without a build, in the `[bar]` section of its [preferences file](#preferences), which applies as it is saved.

## Create one

`winshell new` lists what it can copy: the starter, and each example, such as the Omarchy-style panel. `winshell new <name>` copies one into your config folder.

- **Where it goes.** The copy goes to `%APPDATA%\winshell\<name>`, or under `WINSHELL_CONFIG_DIR`. `--path <folder>` puts it somewhere else. A folder that exists and isn't empty is refused.
- **The package.** The copy's project references the `Winshell` package of the same release as `winshell`. A release keeps that package in a `packages` folder beside `winshell.exe`, and the copy's `nuget.config` adds that folder as a source.
- **Its shell id** comes from its folder, so two copies keep their data apart.

When Winshell runs a configuration:

- **Building.** Winshell builds to `bin\shell` in the project folder when a source is newer, then starts that program. `--no-build` runs the program the last build left, and does not build. A failed build does not start the last good build.
- **A `winshell` and a shell of different releases refuse each other**, except `kill`. The message names both releases.

### From a clone

The `Winshell` package is not published, so a `winshell` [built from a clone](development.md#build-from-source) has none beside it, and `winshell new` says so. Pack it from that clone, which needs PowerShell 7 and Git:

```powershell
git clone https://github.com/enpaze/winshell.git
cd winshell
pwsh scripts/pack.ps1
```

`scripts/pack.ps1` writes the `Winshell` library package to `artifacts\packages`. Then make the project beside the clone, and tell NuGet where the `Winshell` package is:

```powershell
cd ..
winshell new starter --path MyShell
cd MyShell
dotnet new nugetconfig
dotnet nuget add source ..\winshell\artifacts\packages --name winshell --configfile nuget.config
winshell run --path .
```

## Where a configuration lives

A configuration is a .NET project. Winshell looks for a `.csproj` in this order. With none, `winshell run` starts nothing and says nothing.

| What you run | The project it builds and runs |
| --- | --- |
| `winshell run --path <folder or file>` | That `.csproj`, or the one `.csproj` in that folder |
| `winshell run --config <name>` | The project in `%APPDATA%\winshell\<name>` |
| `winshell run` | The configuration `winshell use` chose; else the project in `%APPDATA%\winshell`; else `%APPDATA%\winshell\default`; else the one subfolder of `%APPDATA%\winshell` that has a project. With several, Winshell asks you to choose one with `winshell use`. With none, nothing runs. |

`WINSHELL_CONFIG_DIR` replaces `%APPDATA%\winshell` in this table.

The sign-in entry and the Start tile start what `winshell run` would. With several configurations, such as two copies from `winshell new`, `winshell use <name>` chooses the one they start, and switches to it in one step:

```powershell
winshell new omarchy-style
winshell new starter
winshell use starter          # sign-in starts starter; a shell that is running gives way to it
winshell use omarchy-style    # and back
winshell use                  # lists the configurations, with * before the chosen one
```

- `winshell use` builds the configuration first when it needs a build, so the next sign-in can start it. A build that fails changes nothing.
- The choice is the name in `chosen.txt` in the config folder.
- Choosing a configuration never changes its shell id, so its data, its state, its widgets and where they are placed stay its own through every switch.

## The package's usings

A project that references the `Winshell` package and has `ImplicitUsings` on, as the starter does, gets these global usings. Its files need no `using` line for them:

| Using | What it brings |
| --- | --- |
| `Winshell` | `Shell`, `Bar`, `Widget` and the other types in the [reference](reference/winshell.md) |
| `Microsoft.UI.Reactor` and `Microsoft.UI.Reactor.Core` | `Component`, `Element` and the rest of Reactor |
| `static Microsoft.UI.Reactor.Factories` | Element functions, such as `TextBlock` and `Button` |
| `static Winshell.Elements` | `Surface` and `WidgetArea` |
| The project's `RootNamespace` | The project's own types, in every file |

- **Leave them out.** `<WinshellImplicitUsings>false</WinshellImplicitUsings>` in the project file leaves out all of them. `<Using Remove="Microsoft.UI.Reactor.Core" />` in an `ItemGroup` leaves out one.
- **The root namespace always exists.** The package declares the project's `RootNamespace`, so a shell whose only file is `Program.cs` builds.
- **Two types share a name.** `ColorScheme` is Winshell's, for `shell.ColorScheme`, and Reactor's, for `UseColorScheme()`. Write `Winshell.ColorScheme` or `Microsoft.UI.Reactor.Core.ColorScheme`. A file with `using Microsoft.UI.Xaml.Controls` has two `IconElement` and two `MenuFlyoutItemBase` types the same way.

## The program

`Shell.Run` runs until the shell quits. Call it from top-level statements, as the starter's `Program.cs` does. The function you pass runs once the shell is up, with that shell, which `Shell.Current` also returns:

```csharp
Shell.Run(shell => _ = new Bar { Center = ["clock"] });
```

WinUI needs a single-threaded apartment (STA) thread. When the thread that calls `Shell.Run` is not one, as a top-level program's is not, the shell runs on a new STA thread with the caller's culture, and `Shell.Run` waits for it.

When `start` throws, Winshell writes a crash report and closes the shell. `Shell.Run` then returns without an exception, and the program exits with code 1, which `winshell run` does not restart. An exception that escapes the shell outside `start` comes back out of `Shell.Run` with its original stack.

Properties in the project file name the shell and where it keeps its files. The package puts them in the program when it builds:

```xml
<PropertyGroup>
  <ShellName>my-shell</ShellName>
  <ShellDataDir>data</ShellDataDir>
</PropertyGroup>
```

- **`ShellName`** names the data, state and cache folders. Without it, as in the starter, a shell project in the config folder is named after its folder, and any other after its path.
- **`ShellDataDir`, `ShellStateDir` and `ShellCacheDir`** replace those folders. A relative path is in the project folder, so `data` is `<project>\data`, and a rebuild of `bin\shell` leaves it there. A program a reload built in `bin\shell.next` uses that same folder. A program in neither keeps a relative path next to itself. `%NAME%` expands when the shell starts, and `$BASE` is the folder above `by-shell`, such as `%LOCALAPPDATA%\winshell\data`. MSBuild reads `%` and two hexadecimal digits as one escaped character, so write `%25DEVROOT%` for `%DEVROOT%`.

## A window

Windows stay Winshell's. The content is any `UIElement`. Sizes are logical pixels.

```csharp
        _ = new PanelWindow
        {
            Anchors = Edges.Left | Edges.Right | Edges.Bottom,
            Height = 48,
            ReservedSize = 48,
            Content = new TextBlock { Text = "Winshell" },
            Visible = true
        };
```

- **[PanelWindow](reference/winshell.md#panelwindow)** sits on the edges of a screen. `Anchors` chooses the edges. `ReservedSize` reserves that edge, so maximized windows stop short of it. A `ReservedSize` of 0 reserves nothing. `AboveWindows` set false puts it behind other windows.
- **[PopupWindow](reference/winshell.md#popupwindow)** is a frameless window over the others, placed as [Place a popup window](#place-a-popup-window) describes. `GrabFocus` gives it the keyboard. `Dismissed` fires when it closes.
- **[FloatingWindow](reference/winshell.md#floatingwindow)** is a normal window with a title bar.

On any window:

- **`Backdrop`** is `WindowBackdrop.Mica`, `Acrylic`, `MicaAlt` or `None`.
- **`Corners`** rounds the window.
- **A flyout** is a WinUI `Flyout` in the window's content. Winshell closes an open flyout on a window that does not activate.
- **`Toggle()`** shows a hidden window and hides a shown one.

### Show a component

`PanelWindow<T>`, `PopupWindow<T>` and `FloatingWindow<T>` show the component `T`. The window makes one `T` and puts it on a [surface](#a-surface-of-your-own). Hiding the window keeps the component, and disposing the window unmounts it. The Winshell package carries Reactor, so a shell adds no Reactor reference.

```csharp
        var calendar = new PopupWindow<Calendar> { Position = PopupPosition.Top, Width = 320, Height = 240 };
        var hotkey = shell.Hotkey("Win+Alt+C", calendar.Toggle);
```

A component with props, a `Component<TProps>`, goes in a `PanelWindow<T, TProps>`, `PopupWindow<T, TProps>` or `FloatingWindow<T, TProps>`, which is given the props. Two windows of one component can show different things:

```csharp
        var today = new PopupWindow<Note, string>("Stand-up at 10") { Position = PopupPosition.Top };
        var later = new PopupWindow<Note, string>("Pay the rent");
```

The component renders again when a service object it observes with `UseObservable` changes. `UseObservableTree` follows a whole service, as [Keep a component current](#keep-a-component-current) describes.

### Place a popup window

A popup window's `Position` says where it goes. Its `Width` and `Height` size it, and without them it is 100 by 100:

- **`PopupPosition.Center`**, the default, centers it on the screen with the active app window.
- **`PopupPosition.Top`** centers it across that screen, a fifth of the way down the work area. A launcher opens there.
- **`PopupPosition.At(point)`** puts its top-left corner at a point on the desktop in logical pixels, such as a window's `Origin`.

With no active app window, `Center` and `Top` use the primary screen. A `Screen` set on the window chooses the screen instead.

### One panel per screen

A [ScreenWindows](reference/winshell.md#screenwindows) keeps one window on each screen for a component. It is given the component's name and a function that makes the window for a screen:

```csharp
        var dock = new ScreenWindows("dock", (screen, row) => new PanelWindow
        {
            Screen = screen,
            Anchors = row?.GetValueOrDefault("edge") is "top"
                ? Edges.Left | Edges.Right | Edges.Top
                : Edges.Left | Edges.Right | Edges.Bottom,
            Height = 48,
            ReservedSize = 48,
            Content = new TextBlock { Text = screen.Name },
            Visible = true
        });
```

- It makes a window for each screen when it is made, makes one for a screen that is connected, and disposes the window of a screen that is removed. The other screens' windows stay as they are, and so do their widgets.
- A screen whose resolution or scale changes keeps its window, made with the screen as it was then: `shell.Screens` has it as it is now.
- `Dispose` removes every window.
- The function can return anything disposable that holds the window, such as the window itself. A [component window](#show-a-component) unmounts its component when it is disposed.
- A function that throws is logged under the component's name, and that screen has no window until the screens or the rows change.

A screen that differs from the rest has a row in the component's section of the [preferences file](#preferences), and the function is given that row, or null:

```toml
[[dock.screen]]
manufacturer = "DEL"
model = "DELL U2720Q"
serial = "ABC123"
edge = "top"
```

- **Matching.** A row matches a screen when its `manufacturer`, `model` and `serial` are strings that all equal the screen's, and the first match wins. A key the row leaves out matches nothing, so an empty serial is written `serial = ""`. The name Windows numbers, such as `DISPLAY1`, is never matched, because Windows renumbers it.
- **The identity to copy.** `winshell log` prints a line per screen under the component's name, `manufacturer / model / serial`.
- **What the function is given.** The whole row, the three keys included, as a dictionary of TOML values. The rest of it is the component's.
- **When a row changes.** Only that screen's window is disposed and made again, once the whole save is read.
- **A row for a screen that is not connected** stays in the file.

It is a shortcut: a configuration can still make any window on any screen itself. `shell.Screens` lists the screens, the primary first, and changes as monitors are connected and removed.

## A bar on every screen

A [Bar](reference/winshell.md#bar) is a panel along one edge of every screen, with three [widget areas](#widgets): start, center and end. Each list names widgets by their [names](#widgets). A plain string is a placement with no settings:

```csharp
        var bar = new Bar
        {
            Edge = Edges.Top,
            Start = ["workspaces"],
            Center = ["clock"],
            End = ["tray", new("message", new Dictionary<string, object> { ["text"] = "Hello" })]
        };
```

- **When it shows.** It shows on the next turn of the loop, once its properties are set, on every screen, and follows screens as they are connected and removed, as [ScreenWindows](#one-panel-per-screen) does. `Dispose` removes it.
- **Its edge.** A bar reserves its edge, so maximized windows stop short of it, and `Reserve = false` lays it over them instead. It leaves Explorer's taskbar alone: `shell.ExplorerTaskbar.Hidden = true` hides that.
- **Its thickness.** A bar is as thick as the [style's](#style) `ShellSlotSize`: the height of a top or bottom bar, and the width of a left or right one, whose areas run from top to bottom. `Thickness` sets another size.
- **Its padding.** The style's `ShellPadding` keeps space between the areas and each end of the bar: at its left and right, or at the top and bottom of a side bar. `Padding` sets another space.
- **Its style.** A bar's own `Style` wins over the shell's, as an area's does.

`Bar` uses only the public API a configuration has, and a test holds it to that, so [its source](https://github.com/enpaze/winshell/blob/main/src/Winshell.Shell/Compositions/Bar.cs) is a start for a dock, a sidebar or a bar of your own.

### Several bars

A shell can have several bars. Each has a `Name`, `"bar"` unless given, which names its section of the [preferences file](#preferences):

```csharp
        var side = new Bar { Name = "side", Edge = Edges.Left, Reserve = false, Start = ["clock"] };
```

### Let the user change a bar

A bar's section of the [preferences file](#preferences) holds `edge` and the three lists, each a list of placements like a [named area's](#let-the-user-change-an-area) `widgets`. A key the file has replaces the property of the same name, and a missing file is written with the bar's own values commented out. A `[[bar.screen]]` row, matched as `ScreenWindows` matches it, may change any of the four keys for one screen:

```toml
[bar]
edge = "top"
end = ["tray", { widget = "message", text = "Hello" }]

[[bar.screen]]
manufacturer = "DEL"
model = "DELL U2720Q"
serial = "ABC123"
edge = "left"
end = []
```

Saving applies it live:

- A changed row replaces that screen's bar, and the other screens keep theirs.
- A changed section replaces the bar on every screen, which starts its widgets again.
- A new `shell.Style` restyles a bar without a style of its own in place, so its widgets keep their state.
- A value in the section that can't be used is logged under `preferences` with its line, and the bar keeps the one it had. One in a row is logged under the bar's name, and that screen uses the section's.

### A surface of your own

A bar's areas sit in one [Surface](reference/winshell.md#surface) component, which paints the [style's](#style) `ShellBackgroundBrush` behind them in the window's light or dark theme, and follows a switch while the shell runs. A brush your code reads from the style comes back in the theme the app started in, so a [component window](#show-a-component) puts its component on a surface too:

```csharp
public sealed class Dock : Component
{
    public override Element Render() =>
        WidgetArea("dock", ["start", "tasks"]).Padding(4);
}
```

```csharp
        var dock = new PanelWindow<Dock> { Anchors = Edges.Bottom, Height = 56, Visible = true };
```

`Surface(content, padding, style)` paints part of a component in a style of its own. `Surface` and `WidgetArea` come from `Winshell.Elements`, one of [the package's usings](#the-packages-usings). The props are immutable: rendering the component again with a new record is how the tree changes, and disposing the window unmounts it. What a surface holds stays loaded when the style changes. Its own style wins over the shell's, as an area's does. The area inside the tree is an element, not a second host.

## A launcher

A [Launcher](reference/winshell.md#launcher) is a search box that a hotkey opens over the other windows. Typing searches the apps Start lists, ranked as [`shell.Apps.Search`](reference/winshell-apps.md) ranks them, and the commands you give it, together. Up and Down choose a result, and Enter runs it.

```csharp
        var launcher = new Launcher
        {
            Hotkey = "Alt+Space",
            Commands =
            [
                new("Lock", () => shell.Session.Lock()),
                new("Reload shell", () => shell.Reload())
            ]
        };
```

- **When it opens.** Its [hotkey](reference/winshell-input.md) opens it and closes it again. It opens centered near the top of the screen with the active window, or the primary screen. `Show`, `Hide` and `Toggle` do the same from code, such as from a button in a bar. `Dispose` removes it and frees the hotkey.
- **What it shows.** With nothing typed, it shows the apps used most recently, then the commands. A command goes before the first app whose name matches what was typed less well. It shows the first eight.
- **When it closes.** Enter, or a click on a result, runs it and closes the launcher. Escape and a click outside it close it without running anything.

The launcher's section of the [preferences file](#preferences), `[launcher]` unless `Name` names another, holds `hotkey`, so the user can choose another one:

```toml
[launcher]
hotkey = "Ctrl+Shift+Space"
```

`Launcher` uses only the public API a configuration has, as `Bar` does, so [its source](https://github.com/enpaze/winshell/blob/main/src/Winshell.Shell/Compositions/Launcher.cs) is a start for a launcher of your own.

## Widgets

A [widget area](reference/winshell.md#widgetarea) is a component. A window mounts the component that renders it, and the same element can sit inside a surface. It places widgets by name.

The shell finds the widgets when it starts, before the function passed to `Shell.Run` runs. A widget is a public class that derives from `Widget` and has a public parameterless constructor, in your project or in a project or package it references that references Winshell. An abstract or generic class is not a widget, and neither is a class that is not public.

A widget's name is its class name in kebab case, without a trailing `Widget`: `ClockWidget` and `Clock` are both `clock`, `NetworkSpeed` is `network-speed`, and `CPUUsage` is `cpu-usage`. Names are case-sensitive. `[WidgetName]` gives a widget another name. Two widgets with one name stop the shell when it starts, with an error that names both classes, and so does a widget with no public parameterless constructor.

A widget derives from [Widget](reference/winshell.md#widget), a component whose props are a `WidgetContext`. `Render` reads them without `Props`:

- `Shell` is the running shell, and `Settings` the placement's settings.
- `Orientation` is the area's, and `Style` the style in effect for it, never null.
- `Window` is the shell window the area is in, and null until it is in one.
- `Setting(key, fallback)` reads one setting the way a [preferences](#preferences) key of the fallback's type is read: the fallback when the placement has no such key, and an `UnsupportedWidgetException` that names the key and the type it must be when the value does not convert. `Setting("icons", false)` takes `true` or `false`, or a number, which is true unless it is zero or `nan`. Any other value is refused with `icons must be true or false.`
- `UseNow(every)` renders the widget again every `every` and returns the local time.
- `UseScreen()` returns the screen of `Window` and renders the widget again when it changes. It is null until the area is in a window, and for a window that names no screen, which sits on the primary screen.
- `StylePopup(content)` styles a [popup](#place-widgets-in-an-area).

This widget is `hello`, not `greeting`:

```csharp
[WidgetName("hello")]
public sealed class Greeting : Widget
{
    public override Element Render() =>
        TextBlock(Setting("text", "Hello"));
}
```

For a [widget library](#write-a-widget), add a project or package reference. Its widgets have names the same way, so the library's `Message` is `message`.

### Move a configuration

This release finds only widgets that derive from `Widget`. Three things move, and nothing reads the old shape.

A widget was a control whose constructor took a `WidgetContext` and built its elements there. It is a public `Widget` whose `Render` returns those elements, and the shell finds it by its class name. The old constructor argument is now the widget's own: `Shell`, `Settings` and `Orientation`, and `Window` for the shell window the area is in.

A Watch block read services and then wrote the controls. That read moves into `Render`. `UseObservable` or `UseObservableTree` names the service, and the elements `Render` returns already show what it holds. The [battery](#a-battery) is that shape: it reads `Shell.Power.DisplayDevice` and returns a text block, collapsed when no battery is present.

A XAML resource dictionary of `Shell*` keys becomes a [ShellStyle](#style). A dictionary that set the background and the font becomes `shell.Style` with those values, and the colors it left out keep following Windows. A bar or an area that pointed at a XAML file sets its own `Style` to a `ShellStyle` instead:

```xml
<SolidColorBrush x:Key="ShellBackgroundBrush" Color="#1A1B26" />
<FontFamily x:Key="ShellFontFamily">Segoe UI</FontFamily>
```

```csharp
        shell.Style = new ShellStyle
        {
            Background = Color.FromArgb(255, 0x1a, 0x1b, 0x26),
            FontFamily = new FontFamily("Segoe UI")
        };
```

Nothing reads the XAML file. A control Reactor has no element for is embedded with `XamlHost`, inside the component, and it still takes the shell style.

### Place widgets in an area

Each placement gives its widget the running `Shell`, the shell window the area is drawn in, that placement's settings and the area's orientation. `Window` is null until the area is in a window, and the widget renders again once it is. A widget that shows the screen reads `UseScreen()`, which also follows the window to another screen. One instance serves a placement, rendered again when the settings, the orientation or the window change. Several placements may use one widget:

```csharp
public sealed class EndArea : Component
{
    public override Element Render() => WidgetArea("end",
    [
        new("message", new Dictionary<string, object> { ["text"] = "Hello" }),
        new("message", new Dictionary<string, object> { ["text"] = "World" })
    ]);
}
```

```csharp
        var panel = new PanelWindow<EndArea> { Height = 48, Visible = true };
```

- **Replacing widgets.** Render the area again with a new list, unless the [preferences file](#preferences) has a list for it. A widget whose name is no longer in that place is disposed and made again. A widget that stays is rendered again with the new props, and a new orientation does the same.
- **Disposing an area.** `panel.Dispose()` unmounts the area, which disposes its widgets. Hiding the panel, or a user closing it, keeps them.
- **Settings.** Treat placement settings, including nested tables, as read-only.
- **Styling.** The [shell style](#style) styles the plain controls in an area, including the ones a component renders, so a component names no brush of its own. A widget that still has XAML uses `{ThemeResource}` for area-sensitive resources: `{StaticResource}` resolves at construction, before the area owns the widget.
- **Popups.** Assign `StylePopup(content)` to a `PopupWindow.Content`, or to a flyout's content. A widget may call it as soon as it renders, before the area is in a host. The call loads a fresh dictionary of the area's style at once and follows later changes to that style, until the area removes the widget or is disposed. `content` may be a plain element or a `ReactorHostControl` showing a component. The widget still owns its popup windows.
- **A widget that fails.** An unknown widget, or a component that throws while it is created or while it renders, shows a placeholder with its name and reason while the other placements keep running. The exception and its stack trace are logged under `widgets` in the shell's log (`winshell log`). Throw `UnsupportedWidgetException` with a reason when a widget cannot run in its placement, such as a vertical area. Other exceptions after creation follow the shell's normal crash supervision.

### Let the user change an area

Pass an area's name to let the user replace its placements. The section is created when the component renders. It holds one `widgets` list. Names are case-sensitive, and areas with the same name use the same section. An area with no name always uses its defaults. A missing file is written with each named area's defaults commented out, a placement with no settings as its bare name. Uncomment the list and edit it:

```toml
[end]
widgets = [
  "clock",
  { widget = "message", text = "Hello from the preferences file" },
  { widget = "message", text = "A second placement" },
]
```

- The list replaces the area's **whole** default list, in order.
- A string places the widget of that name with no settings.
- In a table, `widget` names the widget, and the other keys become its `Settings`, including nested tables and arrays.
- `widgets = []` leaves the area empty.
- Comment the list out or remove it to return to the author's defaults. An area absent from the file keeps its defaults.

Saving applies changes live. A widget whose name stays is rendered again with the new settings. A different list of names replaces those widgets. Other areas keep theirs. A syntax error, or a list with an item that is neither a widget name nor a table with a string `widget`, keeps the last valid list and logs the file location under `preferences`. Winshell does not migrate or merge user lists into new defaults. The old placement file, `widgets\<shell id>.toml`, is not read.

## Style

The style is how every widget area, surface and styled popup looks: its colors, font and slot size. It is a [ShellStyle](reference/winshell.md#shellstyle). `shell.Style` starts as `new ShellStyle()`, which follows Windows: its text and background colors in light, in dark and in a contrast theme, Segoe UI and Segoe Fluent Icons. Set `shell.Style` to give the whole shell another look. Set only the values you change: `Background`, `Foreground` and `Alert` are `Windows.UI.Color?` values, and each one left null follows Windows on its own, so `new ShellStyle { FontSize = 12 }` keeps Windows' colors:

```csharp
        shell.Style = new ShellStyle
        {
            Background = Color.FromArgb(255, 0x1a, 0x1b, 0x26),
            Foreground = Color.FromArgb(255, 0xa9, 0xb1, 0xd6),
            Alert = Color.FromArgb(255, 0xf7, 0x76, 0x8e),
            FontFamily = new FontFamily("ms-appx:///Fonts/JetBrainsMonoNerdFont-Regular.ttf#JetBrainsMono NF"),
            FontSize = 12,
            SlotSize = 26,
            Padding = 8
        };
```

A new value reaches what is already on screen, and `new ShellStyle()` returns to Windows' look. `shell.Style` is never null. A bar's, an area's or a surface's own `Style` wins over the shell's, except for tooltips, and `null` there follows the shell. A color the style sets is the same in light and in dark. A contrast theme wins over every color: the background, foreground and alert color become Windows' window, window text and highlight colors.

Winshell writes these resources from the style. A bar and a popup read them with `{ThemeResource}`:

| Key | Style value | What it is | Default |
| --- | --- | --- | --- |
| `ShellBackgroundBrush` | `Background` | Behind a surface, such as a bar's, and a popup | Windows' solid background |
| `ShellForegroundBrush` | `Foreground` | Text and glyphs | Windows' primary text color |
| `ShellAlertBrush` | `Alert` | Something that needs attention, such as a network with no internet | Windows' critical color |
| `ShellFontFamily` | `FontFamily` | Text | Segoe UI Variable Text, then Segoe UI |
| `ShellFontSize` | `FontSize` | Text | 14 |
| `ShellSlotSize` | `SlotSize` | One button, and the thickness of a bar | 32 |
| `ShellPadding` | `Padding` | The space at each end of a bar. `null` gives bars none. | 4 |

It also builds the implicit styles, with no `x:Key`, for the plain controls a widget is made of. They apply inside a widget area, in a surface and in a popup styled with `StylePopup`, so a widget that is a `Button` holding a `FontIcon` looks right with no lookup:

- `Button`: flat, at least one slot wide and high, its content centered, in the style's foreground, font and font size.
- `TextBlock`: the style's foreground, font and font size, centered vertically. A text block keeps that foreground inside a pressed or disabled button, so give a button its text as a string to take the button's colors.
- `FontIcon`: Segoe Fluent Icons at 16. Its color comes from what holds it, so a button's pressed and disabled colors reach the glyph.
- `ToolTip`: the style's background, foreground and font. A tooltip opens in a popup of its own, outside the area, and WinUI looks up its style only in the app's resources, so Winshell puts the `ToolTip` entry of `shell.Style` there. It styles every tooltip in the shell, in your own windows too, and an area's own `Style` does not change it. A `ToolTip` style you put in `Application.Current.Resources` yourself wins over it.

To show an element in the alert color, call `.Alert()` on it. The buttons, text blocks and glyphs in it take `ShellAlertBrush` in place of `ShellForegroundBrush`, and keep the rest of their implicit styles. The color is the area style's `Alert`, Windows' critical color in light and in dark when `Alert` is null, or Windows' highlight color in a contrast theme, and it changes when the theme does:

```csharp
public sealed class AlertLabel : Widget
{
    public override Element Render() => TextBlock("!").Alert();
}
```

`.Alert(false)` shows the element in its usual color, so `.Alert(restricted)` shows it in the alert color only while `restricted` holds.

## Write a widget

A widget other people use can be a class library: a WinUI library that references the Winshell package. The package imports the props the library needs and adds [its usings](#the-packages-usings). A configuration references the library and places its widget by name, as [Widgets](#widgets) describes. The example on this page is `Message`, a widget that shows one line of text, with one `text` setting.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0-windows10.0.22621.0</TargetFramework>
    <RootNamespace>MessageWidget</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Winshell" Version="0.7.0" />
  </ItemGroup>
</Project>
```

`0.7.0` stands in for the release you built the widget against. The package is not on nuget.org. A release keeps it in a `packages` folder beside `winshell.exe`, and `winshell new` writes a `nuget.config` that adds that folder.

The widget takes the shell's [style](#style), because a plain text block does. There is no XAML file. The class is in `MessageWidget`, the project's root namespace, which the package also adds as a using:

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MessageWidget;

public sealed class Message : Widget
{
    public override Element Render()
    {
        var popup = UseRef<PopupWindow?>(null);
        UseEffect(() => () =>
        {
            popup.Current?.Dispose();
            popup.Current = null;
        }, false);

        var screen = UseScreen();
        var text = Setting("text", "Hello");
        if (Orientation == Orientation.Vertical)
            throw new UnsupportedWidgetException("The message widget runs in a horizontal area.");

        var line = text + " ·";
        return TextBlock(line)
            .Padding(8, 0)
            .ToolTip(screen is null ? "primary screen" : screen.Name)
            .OnPointerPressed((_, _) => ShowPopup(popup, line));
    }

    private void ShowPopup(Ref<PopupWindow?> popup, string line)
    {
        popup.Current?.Dispose();
        var body = new TextBlock { Text = line, Margin = new Thickness(12) };
        popup.Current = new PopupWindow { Content = StylePopup(body), Visible = true };
    }
}
```

`text` is the placement's setting, and `Hello` when the placement has none. The line is that string followed by a middle dot. A value that is not a string throws [UnsupportedWidgetException](reference/winshell.md#unsupportedwidgetexception) with `text must be a string.`, and the widget throws one itself in a vertical area. The area shows the widget's name and that reason, and the other placements keep running.

[StylePopup](reference/winshell.md#widget) wraps content for a [PopupWindow](reference/winshell.md#popupwindow). Assign the result to the popup's `Content`. The wrapper loads the area's style and follows its changes. The effect disposes the popup when the component goes, and a press disposes the previous one first.

`UseScreen()` is null until the area is in a window, and the widget renders again when the window arrives and when it moves to another screen. The tooltip names that screen, and says `primary screen` until there is one.

## A service

Services hang off `Shell`, created when first read. `shell.Audio`, `shell.Power`, `shell.Apps`, `shell.WindowList`, `shell.Networking`, `shell.Bluetooth`, `shell.Notifications`, `shell.SystemTray`, `shell.Media` and the others are on the [Winshell](reference/winshell.md#shell) page, and each namespace has its own page.

Reading does not ask for permission and does not change anything. Wi-Fi scanning, notification access and Bluetooth pairing ask Windows only when the configuration calls the method, from a user's action. `shell.Session` can ask Windows to lock. Windows' own lock screen takes over.

`shell.Hotkey(sequence, pressed)` is a hotkey that works anywhere in Windows. The sequence uses `Win`, `Ctrl`, `Alt` and `Shift`, and `pressed` runs each time the user presses it. Dispose the hotkey to give the keys back.

```csharp
        var hotkey = shell.Hotkey("Win+E", () => { });
```

`shell.Open` opens a Settings page, an app or a URL as Windows would: `ms-settings:sound`, `https://example.com` or an app's path. It takes a URI or a path as a string, not a command line.

```csharp
        var sound = new Button { Content = "Sound" };
        sound.Click += (_, _) => shell.Open("ms-settings:sound");
```

It opens off the shell's thread, so a slow app does not hold up the shell. What Windows can't start, such as a path that is not there, is logged under `shell`, not thrown into the click handler. A scheme no app handles shows Windows' own app picker.

`shell.Log` writes to the instance log, which `winshell log` prints.

### Keep a component current

`UseObservableTree` follows a service: a component renders again when anything that service holds changes, including an item in one of its lists. `UseObservable` follows one service object or one list. Both take an `INotifyPropertyChanged` source, which a service object and an [ObservableList](reference/winshell.md#observablelist-of-t) are, and return that source:

```csharp
public sealed class BatteryLabel : Widget
{
    public override Element Render()
    {
        var battery = UseObservable(Shell.Power.DisplayDevice);
        var charge = battery.IsPresent ? $"{battery.Percentage:P0}" : "";
        return TextBlock(charge);
    }
}
```

A change raises `PropertyChanged` on the shell's thread, and the component renders again. A component that observes nothing renders once. A service list, such as `shell.Power.Devices`, is an `ObservableList`. An item's change raises that list, so `UseObservable` on the list renders again when an item changes in place, and `UseObservableTree` on the service does too, because it follows the list. Only its service changes the list. A class of your own that derives from `ObservableObject` raises `PropertyChanged` from each setter, as in `set => Set(ref field, value);`. Following one list under two properties of that object throws, because the second property would never notify.

## Preferences

A shell keeps the user's choices in one TOML file, `preferences\<shell id>.toml` in `%APPDATA%\winshell`, or in `WINSHELL_CONFIG_DIR` when that is set. It lives outside the shell project and survives shell updates. The configuration's own preferences are the keys before the first section, and each component, such as a [named widget area](#let-the-user-change-an-area), has a section named after it:

```toml
clock24 = true

[dock]
edge = "left"

[end]
widgets = [{ widget = "message", text = "Hello" }]
```

A class that derives from `Preferences` declares a section. It has one public read-write property per key, and the key is the property name with a lowercase first letter. A key takes a string, a number, `true` or `false`, a list or a table. A `bool` key also takes a number, which is `false` when it is zero or `nan` and `true` otherwise. An `int` key takes a whole number that fits, and a `double` key any number. The property's initializer is its default, and `TomlComment` is the comment written above it. Made with no name, the class holds the configuration's own keys:

```csharp
public sealed class ShellPreferences : Preferences
{
    [TomlComment("Show 13:00 rather than 1:00 PM.")]
    public bool Clock24
    {
        get => Read(field);
        set => Set(ref field, value);
    }
}
```

A component's class passes its name. The check returns why a value is refused, or `""`:

```csharp
public sealed class DockPreferences(string name) : Preferences(name, Check)
{
    [TomlComment("bottom, left or right.")]
    public string Edge
    {
        get => Read(field);
        set => Set(ref field, value);
    } = "bottom";

    private static string Check(string key, object value)
    {
        return key == "edge" && value is not ("bottom" or "left" or "right") ? "must be bottom, left or right" : "";
    }
}
```

Make one on the shell's thread, and dispose it when its component goes. It reads its section when it is made. [UseObservable](#keep-a-component-current) renders again when a key changes:

```csharp
public sealed class ClockLabel : Component
{
    private readonly ShellPreferences _preferences = new();

    public override Element Render()
    {
        var preferences = UseObservable(_preferences);
        UseEffect(() => () => _preferences.Dispose(), false);
        return TextBlock(preferences.Clock24 ? "13:00" : "1:00 PM");
    }
}
```

A named section is made with its name:

```csharp
        var dock = new DockPreferences("dock");
```

### How the file and the shell stay in step

- **Saving the file applies it live**, and changes only the properties whose keys changed, so a component whose section is the same keeps what it shows.
- **A key the file does not have holds its default**, so commenting one out goes back to it.
- **Assigning a property writes that key** at the end of its section and leaves the rest of the file: comments, other sections and keys no class reads.
- **When the file is missing**, Winshell writes it with each section's keys commented out at their defaults, under their comments. A component made after that, whose section the file does not have, adds its section at the end of the file the same way, under its own `[name]` header.
- **A section you take out stays out** until the component is made again, as when the shell starts or reloads, or one of its properties is assigned.

### What can't be used

What can't be used is logged under `preferences`, with its line, and the values in use stay: a file that does not parse, a section that is not a table, and a value the property can't hold or the check refuses.

- A section no component reads stays in the file and is logged by name.
- An unknown key that differs from a key the class reads only in case, by a letter, or by two in a longer key is logged with that key.
- A file past 1 MiB, or nested past 64 levels, is not read.

`winshell doctor` checks the file of the configuration it checks for TOML errors, and names the line. It finds the file by the shell name the project has without a `ShellName` property, as [The program](#the-program) describes. For a project that sets one, it checks that default file, not the one the shell reads.

### Paths

`shell.Paths` says where the shell's files are:

| Property | Where |
| --- | --- |
| `Data` | `%LOCALAPPDATA%\winshell\data\by-shell\<shell name>`, or the `ShellDataDir` folder |
| `State` | `%LOCALAPPDATA%\winshell\state\by-shell\<shell name>`, or the `ShellStateDir` folder |
| `Cache` | `%LOCALAPPDATA%\winshell\cache\by-shell\<shell name>`, or the `ShellCacheDir` folder |
| `Logs` | This instance's log file, `%LOCALAPPDATA%\winshell\run\<instance id>\log.txt`, which `winshell log` prints |
| `Program` | The folder the program runs from, such as `bin\shell` in the project folder |
| `Shells` | The folder of shell projects and preferences files, `%APPDATA%\winshell` or `WINSHELL_CONFIG_DIR` |

Reading `Data`, `State` or `Cache` creates that folder. The shell works out every path once, when it starts, so they stay put while it runs. Changing `WINSHELL_CONFIG_DIR` or `WINSHELL_LOCAL_DIR` later moves none of them, and the preferences file stays in the `Shells` folder the shell started with. [The program](#the-program) shows the properties that move them. State does not survive a reload unless the shell writes it itself. Every window is replaced.

## IPC

`shell.Ipc` is what `winshell ipc` calls. The handler's own types say what it takes and returns. `string`, `int`, `bool`, `double`, `float` and `Windows.UI.Color` cross the pipe. Nothing is evaluated.

```csharp
        shell.Ipc.Handle("notes", "hello", (string name) => "hi " + name);
```

`winshell ipc call notes hello Ada` prints `hi Ada`. `Property` and `Signal` are on [ShellIpc](reference/winshell.md#shellipc). Dispose the registration to take it away.

`winshell ipc call notice dismiss` closes Winshell's own notice. That call is not a target you register, so `ipc show` does not list it.

## Reload

A configuration can reload itself:

```csharp
        if (!shell.Reload())
            shell.Log.Warn("shell", "Reload did not start.");
```

That starts this program again with `--after` and quits. It does not build, and the new process runs with no supervisor.

`winshell reload` builds first:

- On failure the running instance stays and a notice shows.
- On success the new instance takes over, and under `winshell run` it stays supervised: `winshell run` starts it after this one quits.

There is no file watcher. [`winshell dev`](#develop-a-shell) applies each edit as you save.

`winshell run --no-build` skips the build. The sign-in entry uses it, so signing in does not need the SDK and does not run a broken edit.

## Develop a shell

```powershell
winshell dev --path .
```

`winshell dev` builds your project, starts it, and applies each save to the running shell through `dotnet watch` and Reactor's hot reload, until you press Ctrl+C, which closes that shell and stops `dotnet watch`, as closing the terminal does. It needs the .NET SDK, and it runs a Debug build. With no project it prints `winshell dev needs a configuration project. Run winshell new to make one.` and exits with 1.

What each kind of edit does:

- **A C# edit**, including a component's `Render`, reaches the running shell in about a second. Reactor's hot reload applies it without a restart. The first one takes a little longer, while the compiler warms up. An edited method runs its new code at its next call.
- **A new widget class** is found when the shell next starts, not by hot reload.
- **A widget that shows an edited type** is made again, with the rest of its widget area, so a change to its constructor shows too. The window, its other areas and every other window stay as they are.
- **Your own control that shows an edited type** anywhere else is made again in its place, so a change to a constructor shows there too. That is the outermost of your controls around it, because its constructor builds everything inside. A Winshell or WinUI control around it, such as a `Grid` you fill in code, stays with everything else it holds. A control in a place a new one can't go, such as a template, stays. The window keeps its place, and every other window keeps its content and its state. A control whose constructor takes arguments stays, and its edited methods still run. Keep state that must survive in a model that control is given.
- **An edit .NET can't apply to running code**, such as a new base class, restarts the shell. That takes as long as a build, and the state of every window goes with it.
- **Code that doesn't compile** shows in a notice on screen, and the last version that did keeps running. The notice goes when the next save compiles. Before the first successful build there is no shell to show it, so the error is in the console.
- **A TOML file with an error** shows in the same notice, and what came from it before stays in use. Without `winshell dev` it goes to the log.

Desktop apps you open from the shell stay open when `winshell dev` builds and starts the shell again, and when `winshell dev` ends, as they do after `winshell kill`.
