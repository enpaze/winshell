# Configuration guide

A configuration is a .NET project that references the Winshell package and builds into one program: your shell. This page begins with the starter, a bar with a clock and a battery, and reads it file by file. The sections after that explain each part in full.

- The [reference](reference/index.md) lists every type and member.
- The [Omarchy-style example](https://github.com/enpaze/winshell/tree/main/examples/omarchy-style) is a whole panel to copy from.
- The C# on this page is copied from files that build: the starter, [GuideSamples.cs](https://github.com/enpaze/winshell/blob/main/tests/Winshell.Shell.Tests/GuideSamples.cs) and the [message widget](samples/message-widget/Message.cs). Nothing in GuideSamples runs.

## Your first bar

You need the [.NET SDK](https://dotnet.microsoft.com/download). Make the starter and run it:

```powershell
winshell new starter
winshell run -c starter
```

A bar shows along the top of every screen, with the time in the middle and the battery's charge at the end. It reserves that edge, so maximized windows stop short of it. `winshell kill` closes it. The project is in `%APPDATA%\winshell\starter`, and the C# below is its own text.

### The shell

`AppShell.cs` says what the shell shows. `Start` runs once the shell is up, and makes one [bar](#a-bar-on-every-screen):

```csharp
public sealed class AppShell : ShellConfiguration
{
    public override void Start(Shell shell)
    {
        // "clock" and "battery" are the names these components are registered under.
        Widgets.Register<Clock>("clock");
        Widgets.Register<Battery>("battery");
        _ = new Bar { Edge = Edges.Top, Center = ["clock"], End = ["battery"] };
    }
}
```

The bar keeps itself on screen, so nothing needs to hold it. `Edges.Bottom` puts it at the bottom, and the bar's `Start` is a third list, at the left. With no [style](#style) set, it takes Windows' colors and font, in light or dark.

### A clock

A widget is a Reactor component registered by name, as [Widgets](#widgets) describes. `Render` reads `Props` and returns the controls it shows. The clock renders one text block. The time is not a service, so a timer asks it to render again each second:

```csharp
public sealed class Clock : Component<WidgetContext>
{
    public override Element Render()
    {
        var (time, setTime) = UseState(DateTime.Now);
        // UseObservable follows a service. The time is not one, so a timer renders the clock again each second.
        UseEffect(() =>
        {
            var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (_, _) => setTime(DateTime.Now);
            timer.Start();
            return () => timer.Stop();
        }, false);
        // The name is how inspect and Narrator tell this text from any other. The text itself is only the time.
        return TextBlock($"{time:t}").AutomationName("Clock");
    }
}
```

`{time:t}` is the time as Windows is set to write it, such as `14:05` or `2:05 PM`. The effect runs on the shell's thread, the one thread that may change a control, and its cleanup stops the timer when the clock leaves the tree. `DispatcherQueue.GetForCurrentThread()` is that thread's queue. `AutomationName` is the name `winshell inspect` and Narrator use.

### A battery

The battery observes the shell's `Power` service, which holds what Windows reports:

```csharp
public sealed class Battery : Component<WidgetContext>
{
    public override Element Render()
    {
        var battery = UseObservable(Props.Shell.Power.DisplayDevice);
        return TextBlock($"{battery.Percentage:P0}")
            .Margin(8, 0, 8, 0)
            .IsVisible(battery.IsPresent)
            .AutomationName("Battery");
    }
}
```

`Props.Shell` is the running shell. [UseObservable](#keep-a-component-current) renders the component again when that device changes. A PC with no battery has `IsPresent` false, so the text collapses. The margin keeps it off the screen's edge. A plain text block takes the [style](#style), so the battery names no brush of its own.

`HealthSupported` is true when Windows reported the capacities behind `HealthPercentage`, including when that percentage is 0. A shell that shows health reads the flag first.

`shell.PowerMode.Mode` and `Effective` are `Unknown` when Windows did not report a power mode. Setting `Mode` to `Unknown` does nothing: that is not a position of the slider.

### Register the widgets

`Start` registers each component under the name the bar places. A `WinshellWidget` item does the same for a component from a library, as [Widgets](#widgets) describes.

`Program.cs` starts the shell, and you won't need to change it.

### Change it

Close the bar with `winshell kill`, and run the starter under `winshell dev` instead. It applies each edit to the running bar as you save it, until you press Ctrl+C:

```powershell
winshell kill
winshell dev -c starter
```

Change `{time:t}` in `Clock.cs` to `{time:T}` and save it, and the clock shows the seconds. The bar stays where it is, and so does the battery.

`Start = ["battery"]` in place of `End` moves the battery to the left. Or write a widget of your own: a component like these, and a `Widgets.Register` line for it. [Widgets](#widgets) has the rest, and [A service](#a-service) lists what else the shell can read. Whoever uses the shell can rearrange the bar without a build, in the `[bar]` section of its [preferences file](#preferences), which applies as it is saved.

## Create one

`winshell new` lists what it can copy: the starter, and each example, such as the Omarchy-style panel. `winshell new <name>` copies one into your config folder.

- **Where it goes.** The copy goes to `%APPDATA%\winshell\<name>`, or under `WINSHELL_CONFIG_DIR`. `--path <folder>` puts it somewhere else. A folder that exists and isn't empty is refused.
- **The package.** The copy's project references the `Winshell` package of the same release as `winshell`. A release keeps that package in a `packages` folder beside `winshell.exe`, and the copy's `nuget.config` adds that folder as a source.
- **Its shell id** comes from its folder, so two copies keep their data apart.

When Winshell runs a configuration:

- **Building.** Winshell builds to `bin\shell` in the project folder when a source is newer, then starts that program. `--no-build` runs the program the last build left, and does not build. A failed build does not start the last good build.
- **A downloaded project is refused before any build.** Winshell looks for a `Zone.Identifier` stream on the `.csproj` and on `.props` and `.targets` beside it. Unblock the file (right-click > Properties > Unblock, or `Unblock-File`) if you trust it. `git clone` writes no zone, so it is not caught.
- **A `winshell` and a shell of different releases refuse each other**, except `kill`. The message names both releases.

### From a clone

The `Winshell` package is not published, so a `winshell` [built from a clone](development.md#build-from-source) has none beside it, and `winshell new` says so. Pack it from that clone, which needs PowerShell 7 and Git:

```powershell
git clone https://github.com/enpaze/winshell.git
cd winshell
pwsh scripts/pack.ps1
```

`scripts/pack.ps1` writes the `Winshell` package and the `dotnet new` template to `artifacts\packages`, and `<version>` is the release in their names. Then make the project beside the clone, and tell NuGet where the `Winshell` package is:

```powershell
cd ..
winshell new starter --path MyShell
cd MyShell
dotnet new nugetconfig
dotnet nuget add source ..\winshell\artifacts\packages --name winshell --configfile nuget.config
winshell run --path .
```

`dotnet new install .\winshell\artifacts\packages\Winshell.Templates.<version>.nupkg`, then `dotnet new winshell -n MyShell`, makes the same starter.

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
- `winshell widget` with no `-c` acts on the chosen one.

## The program

`Shell.Run` runs until the shell quits. Call it from `Main`, marked `[STAThread]`, as the starter's `Program.cs` does. With a `ShellId`, the call reads:

```csharp
Shell.Run<AppShell>(new ShellOptions { ShellId = "MyShell" });
```

`AppShell` is a `ShellConfiguration`. `Start` runs once the shell is up. `Shell.Current` is that shell.

`ShellOptions` names where the shell keeps its files:

- **`ShellId`** names the data, state and cache folders. Without it, as in the starter, a configuration in the config folder is named after its folder, and any other after its path.
- **`DataDir`, `StateDir` and `CacheDir`** replace those folders. A relative path is in the project folder, so `data` is `<project>\data`, and a rebuild of `bin\shell` leaves it there. A program a reload built in `bin\shell.next` uses that same folder. A program in neither keeps a relative path next to itself. `%NAME%` expands, and `$BASE` is the folder above `by-shell`, such as `%LOCALAPPDATA%\winshell\data`.

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
- **[PopupWindow](reference/winshell.md#popupwindow)** is a frameless window placed next to another. `GrabFocus` gives it the keyboard. `Dismissed` fires when it closes.
- **[FloatingWindow](reference/winshell.md#floatingwindow)** is a normal window with a title bar.

On any window:

- **`Backdrop`** is `WindowBackdrop.Mica`, `Acrylic`, `MicaAlt` or `None`.
- **`Corners`** rounds the window.
- **A flyout** is a WinUI `Flyout` in the window's content. Winshell closes an open flyout on a window that does not activate.

The Winshell package carries Reactor, so a configuration adds no Reactor reference. Set `new ReactorHostControl(component)` as a panel or popup window's content. The component renders again when a service object it observes with `UseObservable` changes. `UseObservableTree` follows a whole service, as [Keep a component current](#keep-a-component-current) describes.

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
- The function can return anything disposable that holds the window, such as an object that also disposes the window's host, which unmounts its widgets.
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

A [Bar](reference/winshell.md#bar) is a panel along one edge of every screen, with three [widget areas](#widgets): start, center and end. Each list names widgets by the names they are registered under. A plain string is a placement with no settings:

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

A bar's section of the [preferences file](#preferences) holds `edge` and the three lists, each a list of tables like a [named area's](#let-the-user-change-an-area) `widgets`. A key the file has replaces the property of the same name, and a missing file is written with the bar's own values commented out. A `[[bar.screen]]` row, matched as `ScreenWindows` matches it, may change any of the four keys for one screen:

```toml
[bar]
edge = "top"
end = [{ widget = "tray" }, { widget = "message", text = "Hello" }]

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

A bar's areas sit in one [Surface](reference/winshell.md#surface) component, which paints the [style's](#style) `ShellBackgroundBrush` behind them in the window's light or dark theme, and follows a switch while the shell runs. A window's content is one `ReactorHostControl`, and that host mounts the component you write. A brush your code reads from the style comes back in the theme the app started in, so give a window of your own a surface too:

```csharp
public sealed class Dock : Component
{
    public override Element Render() =>
        Surface(WidgetArea("dock", ["start", "tasks"]), new Thickness(4));
}
```

```csharp
        var host = new ReactorHostControl { ComponentFactory = () => new Dock() };
        var dock = new PanelWindow { Anchors = Edges.Bottom, Height = 56, Content = host, Visible = true };
        dock.Closed += host.Dispose;
```

`using static Winshell.Elements` brings `Surface` and `WidgetArea` into scope. The props are immutable: rendering the component again with a new record is how the tree changes, and disposing the host unmounts it. What a surface holds stays loaded when the style changes. Its own style wins over the shell's, as an area's does. The area inside the tree is an element, not a second host.

## Widgets

A [widget area](reference/winshell.md#widgetarea) is a component. A window mounts the component that renders it, and the same element can sit inside a surface. List widgets in your configuration's project, next to their project or package references:

```xml
<ItemGroup>
  <WinshellWidget Include="message" Type="MyShell.MessageWidget" />
</ItemGroup>
```

- `Include` is the case-sensitive placement name.
- `Type` is the C# type name of a `Component<WidgetContext>` with a public parameterless constructor.
- The build generates `Widgets.Register<T>` calls that run before `Main`, without reflection. Names are escaped for C# automatically.
- A missing type, a type that is not a C# type name, a type that is not such a component, and a duplicate name fail the build, and the error names the widget.
- Call `Widgets.Register<T>` yourself for a component the project defines, as the starter does.

A widget is a `Component<WidgetContext>`. `Render` reads `Props.Shell`, `Props.Window`, `Props.Settings` and `Props.Orientation`.

```csharp
        Widgets.Register<ClockWidget>("clock");
```

```csharp
public sealed class ClockWidget : Microsoft.UI.Reactor.Core.Component<WidgetContext>
{
    public override Microsoft.UI.Reactor.Core.Element Render() =>
        Microsoft.UI.Reactor.Factories.TextBlock((string)Props.Settings["text"]);
}
```

The build writes registration code only for an executable, an `OutputType` of `Exe` or `WinExe`. A [widget library](#write-a-widget) can list `WinshellWidget` items so `dotnet msbuild -getItem:WinshellWidget` can read them, and that library does not register the widgets. The configuration that references the library does. Registering one name from both fails when the shell starts.

### Move a configuration

This release registers only component widgets. Three things move, and nothing reads the old shape.

A widget was a control whose constructor took a `WidgetContext` and built its elements there. It is a `Component<WidgetContext>` whose `Render` returns those elements, registered with `Widgets.Register<T>` or a `WinshellWidget` item whose `Type` is that component. `Props` is the old constructor argument: `Props.Shell`, `Props.Settings` and `Props.Orientation`, and `Props.Window` for the shell window the area is in.

A Watch block read services and then wrote the controls. That read moves into `Render`. `UseObservable` or `UseObservableTree` names the service, and the elements `Render` returns already show what it holds. The [battery](#a-battery) is that shape: it reads `Props.Shell.Power.DisplayDevice` and returns a text block, collapsed when no battery is present.

A XAML resource dictionary of `Shell*` keys becomes a [ShellStyle](#style). A dictionary that set the background, the font and the slot size becomes `shell.Style`, and a bar or an area that pointed at a XAML file sets its own `Style` to a `ShellStyle` instead:

```xml
<SolidColorBrush x:Key="ShellBackgroundBrush" Color="#1A1B26" />
<FontFamily x:Key="ShellFontFamily">Segoe UI</FontFamily>
```

```csharp
        shell.Style = new ShellStyle
        {
            Background = Color.FromArgb(255, 0x1a, 0x1b, 0x26),
            Foreground = Color.FromArgb(255, 0xa9, 0xb1, 0xd6),
            Alert = Color.FromArgb(255, 0xf7, 0x76, 0x8e),
            FontFamily = new FontFamily("Segoe UI")
        };
```

Nothing reads the XAML file. A control Reactor has no element for is embedded with `XamlHost`, inside the component, and it still takes the shell style.

### Place widgets in an area

Each placement passes the running `Shell`, the shell window the area is drawn in, that placement's settings and the area's orientation in its `WidgetContext`. `Props.Window` is null until the area is in a window, and the widget renders again once it is, so a widget reads the screen from `Props.Window.Screen`. One instance serves a placement, rendered again with a new `WidgetContext` when the settings, the orientation or the window change. Several placements may use one widget:

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
        var host = new ReactorHostControl { ComponentFactory = () => new EndArea() };
        var panel = new PanelWindow { Height = 48, Content = host, Visible = true };
        panel.Closed += host.Dispose;
```

- **Replacing widgets.** Render the area again with a new list, unless the [preferences file](#preferences) has a list for it. A widget whose name is no longer in that place is disposed and made again. A widget that stays is rendered again with the new props, and a new orientation does the same.
- **Disposing an area.** The example treats a user closing the panel as permanent: `Closed` normally only hides a window, and here it disposes the host, which unmounts the widgets. When your code tears the panel down with `panel.Dispose()`, dispose the host too. Removing or reparenting the host does not dispose widgets.
- **Settings.** Treat placement settings, including nested tables, as read-only.
- **Styling.** The [shell style](#style) styles the plain controls in an area, including the ones a component renders, so a component names no brush of its own. A widget that still has XAML uses `{ThemeResource}` for area-sensitive resources: `{StaticResource}` resolves at construction, before the area owns the widget.
- **Popups.** Assign `context.StylePopup(content)` to a `PopupWindow.Content`, or to a flyout's content. A widget may call it as soon as it has its context, before the area is in a host. The call loads a fresh dictionary of the area's style at once and follows later changes to that style, until the area removes the widget or is disposed. `content` may be a plain element or a `ReactorHostControl` showing a component. The widget still owns its popup windows.
- **A widget that fails.** An unknown widget, or a component that throws while it is created or while it renders, shows a placeholder with its name and reason while the other placements keep running. The exception and its stack trace are logged under `widgets` in the shell's log (`winshell log`). Throw `UnsupportedWidgetException` with a reason when a widget cannot run in its placement, such as a vertical area. Other exceptions after creation follow the shell's normal crash supervision.

### Let the user change an area

Pass an area's name to let the user replace its placements. The section is created when the component renders. It holds one `widgets` list. Names are case-sensitive, and areas with the same name use the same section. An area with no name always uses its defaults. A missing file is written with each named area's defaults commented out. Uncomment the list and edit it:

```toml
[end]
widgets = [
  { widget = "message", text = "Hello from the preferences file" },
  { widget = "message", text = "A second placement" },
]
```

- The list replaces the area's **whole** default list, in order.
- `widget` names the registered widget, and the other keys become its `WidgetContext.Settings`, including nested tables and arrays.
- `widgets = []` leaves the area empty.
- Comment the list out or remove it to return to the author's defaults. An area absent from the file keeps its defaults.

Saving applies changes live. A widget whose name stays is rendered again with the new settings. A different list of names replaces those widgets. Other areas keep theirs. A syntax error or a list without string `widget` names keeps the last valid list and logs the file location under `preferences`. Winshell does not migrate or merge user lists into new defaults. The old placement file, `widgets\<shell id>.toml`, is not read.

## Style

The style is how every widget area, surface and styled popup looks: its colors, font and slot size. It is a [ShellStyle](reference/winshell.md#shellstyle). With none set, the built-in style follows Windows: its text and background colors in light, in dark and in a contrast theme, Segoe UI and Segoe Fluent Icons. Set `shell.Style` to give the whole shell another look. `Background`, `Foreground` and `Alert` are `Windows.UI.Color` values:

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

A new value reaches what is already on screen, and `null` returns to the built-in style. A bar's, an area's or a surface's own `Style` wins over the shell's, except for tooltips, and `null` there follows the shell. A style's colors are the same in light and in dark. A contrast theme wins over them: the background, foreground and alert color become Windows' window, window text and highlight colors.

Winshell writes these resources from the style. A bar and a popup read them with `{ThemeResource}`:

| Key | Style value | What it is | Built-in |
| --- | --- | --- | --- |
| `ShellBackgroundBrush` | `Background` | Behind a surface, such as a bar's, and a popup | Windows' solid background |
| `ShellForegroundBrush` | `Foreground` | Text and glyphs | Windows' primary text color |
| `ShellAlertBrush` | `Alert` | Something that needs attention, such as a network with no internet | Windows' critical color |
| `ShellFontFamily` | `FontFamily` | Text | Segoe UI Variable Text, then Segoe UI |
| `ShellFontSize` | `FontSize` | Text | 14 |
| `ShellSlotSize` | `SlotSize` | One button, and the thickness of a bar | 32 |
| `ShellPadding` | `Padding` | The space at each end of a bar. `null` gives bars none. | 4 |

It also builds the implicit styles, with no `x:Key`, for the plain controls a widget is made of. They apply inside a widget area, in a surface and in a popup styled with `context.StylePopup`, so a widget that is a `Button` holding a `FontIcon` looks right with no lookup:

- `Button`: flat, at least one slot wide and high, its content centered, in the style's foreground, font and font size.
- `TextBlock`: the style's foreground, font and font size, centered vertically. A text block keeps that foreground inside a pressed or disabled button, so give a button its text as a string to take the button's colors.
- `FontIcon`: Segoe Fluent Icons at 16. Its color comes from what holds it, so a button's pressed and disabled colors reach the glyph.
- `ToolTip`: the style's background, foreground and font. A tooltip opens in a popup of its own, outside the area, and WinUI looks up its style only in the app's resources, so Winshell puts the `ToolTip` entry of `shell.Style` there. It styles every tooltip in the shell, in your own windows too, and an area's own `Style` does not change it. A `ToolTip` style you put in `Application.Current.Resources` yourself wins over it.

A widget reads the alert color from the style. With none set, `context.Style` is null and `ShellAlertBrush` follows Windows:

```csharp
        if (context.Style is { } style)
            label.Foreground = new SolidColorBrush(style.Alert);
```

## Write a widget

A widget other people use can be a class library. [samples/message-widget](samples/message-widget) is one, and the solution builds it: a component that shows one line of text, with one `text` setting. It is a sample for this guide, not a widget Winshell ships. A configuration references the library and registers the component the way [Widgets](#widgets) does, with the type `MessageWidget.Message`.

The project is a WinUI library. In this repository it builds against Winshell's source. A library lists a `WinshellWidget` item whose `Type` is the component, so [`winshell widget install`](#installed-widgets) can register it. This sample lists none: the configuration registers `MessageWidget.Message` itself, as [Widgets](#widgets) shows.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- In this repository the sample builds against Winshell's source, so the check builds it. A library you publish
       drops this import and the ProjectReference and references the Winshell package instead. The package imports
       these props. See docs/configuration.md. -->
  <Import Project="..\..\..\src\Winshell.Package\buildTransitive\Winshell.props" />
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>
    <RootNamespace>MessageWidget</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\src\Winshell.Shell\Winshell.Shell.csproj" />
  </ItemGroup>
</Project>
```

That import and the project reference are how this repository builds the sample, the same way the [Omarchy example](https://github.com/enpaze/winshell/blob/main/examples/omarchy-style/OmarchyStyle.csproj) builds. A library you publish references the package instead, and the package imports those props:

```xml
<ItemGroup>
  <PackageReference Include="Winshell" Version="0.7.0" />
</ItemGroup>
```

`0.7.0` stands in for the release you built the widget against. A shell on an older release refuses the install, and the message names both versions. The package is not on nuget.org. A release keeps it in a `packages` folder beside `winshell.exe`, and `winshell new` writes a `nuget.config` that adds that folder. Installing a widget restores the library with the configuration's package sources, so the library needs no `nuget.config` of its own.

The component takes the shell's [style](#style), because a plain text block does. There is no XAML file. `Render` reads the placement as `Props`:

```csharp
public sealed class Message : Component<WidgetContext>
{
    public override Element Render()
    {
        var popup = UseRef<PopupWindow?>(null);
        UseEffect(() => () =>
        {
            popup.Current?.Dispose();
            popup.Current = null;
        }, false);

        var text = TextOf(Props);
        if (Props.Orientation == Orientation.Vertical)
            throw new UnsupportedWidgetException("The message widget runs in a horizontal area.");

        var line = text + " ·";
        return TextBlock(line)
            .Padding(8, 0)
            .OnPointerPressed((_, _) => ShowPopup(popup, line))
            .OnMount(ShowScreen);
    }

    private static string TextOf(WidgetContext context)
    {
        if (context.Settings.TryGetValue("text", out var value) && value is string text)
            return text;
        throw new UnsupportedWidgetException("text must be a string.");
    }

    private void ShowPopup(Ref<PopupWindow?> popup, string line)
    {
        popup.Current?.Dispose();
        var body = new TextBlock { Text = line, Margin = new Thickness(12) };
        popup.Current = new PopupWindow { Content = Props.StylePopup(body), Visible = true };
    }

    private static void ShowScreen(FrameworkElement label)
    {
        void SetTooltip()
        {
            var screen = ShellWindow.Of(label)?.Screen;
            ToolTipService.SetToolTip(label, screen is null ? "primary screen" : screen.Name);
        }

        if (label.IsLoaded)
            SetTooltip();
        else
            label.Loaded += (_, _) => SetTooltip();
    }
}
```

`text` is the placement's setting. The line is that string followed by a middle dot. A value that is not a string throws [UnsupportedWidgetException](reference/winshell.md#unsupportedwidgetexception), and so does a vertical area. The area shows the widget's name and that reason, and the other placements keep running.

[StylePopup](reference/winshell.md#widgetcontext) wraps content for a [PopupWindow](reference/winshell.md#popupwindow). Assign the result to the popup's `Content`. The wrapper loads the area's style and follows its changes. The effect disposes the popup when the component goes, and a press disposes the previous one first.

[ShellWindow.Of](reference/winshell.md#shellwindow) is null until the text is in a window. `OnMount` reads the screen then, or from `Loaded` when the text is not in a window yet. Null is the primary screen.

## Installed widgets

Installed widgets live in `widgets.props`, beside the project file. When that file exists, the build imports it, so `dotnet build`, Visual Studio and `winshell run` include the same widgets. The file is the only record of an install:

- `winshell widget install` writes it.
- `winshell widget list` reads it.
- `winshell widget update` moves an installed source to a newer commit.
- `winshell widget remove` rewrites it.

Install, list, remove and update take `-c` and `-p`. Install and update need `git` and the .NET SDK, and say so when either is missing.

### The widgets file

An empty `<Project />` means nothing is installed. The template ignores the file, and so does the Omarchy example, so an install stays out of the shell's source. With one source installed, it reads:

```xml
<Project>
  <ItemGroup>
    <ProjectReference Include="C:\Users\me\AppData\Local\winshell\widgets\someone\clock-widget\0123abc...\ClockWidget.csproj">
      <WinshellSource>github.com/someone/clock-widget</WinshellSource>
      <WinshellRef>v1.2</WinshellRef>
      <WinshellCommit>0123abc...</WinshellCommit>
      <UndefineProperties>WinshellWidgetsFile</UndefineProperties>
    </ProjectReference>
    <WinshellWidget Include="clock" Type="ClockWidget.Clock">
      <WinshellSource>github.com/someone/clock-widget</WinshellSource>
    </WinshellWidget>
  </ItemGroup>
</Project>
```

- `Include` on the project reference is the project inside a clone of one commit.
- `WinshellSource` is the repository, `WinshellRef` is the branch, tag or commit that was asked for, and `WinshellCommit` is the commit that was cloned.
- `UndefineProperties` names `WinshellWidgetsFile`, so that global property does not flow into the referenced project and the library does not import this file and reference itself.
- Each `WinshellWidget` item follows the reference it came from.

Set `WinshellWidgetsFile` to an absolute path to import a different file: `dotnet build -p:WinshellWidgetsFile=...`. A relative path is resolved from the folder that contains `Winshell.props`, not from the project. The default is `widgets.props` in the project directory.

### Install

`winshell widget install github.com/someone/clock-widget@v1.2` resolves that ref to a commit and clones it under `%LOCALAPPDATA%\winshell\widgets\someone\clock-widget\<commit>`, or under `WINSHELL_LOCAL_DIR` when that is set. A `file://` URL or a folder is cloned the same way, under `widgets\local`.

- **Which project.** With no `--project`, the repository must contain one project that lists `WinshellWidget` items; otherwise the command names the projects and installs nothing.
- **It asks first.** It prints the repository, the commit and the widget names, warns that building runs the repository's code and MSBuild targets in a program that starts at sign-in, and asks. `--yes` skips the question. Declining changes nothing.
- **What it refuses.** A widget whose `Winshell` package reference needs a newer version than the configuration's is refused before that build, and the message names both versions. A widget name already installed from another source is refused, and so is a source that is already installed.
- **The staged build.** Installing builds the configuration against a staged copy, with `-p:WinshellWidgetsFile` set to that copy's absolute path, into a scratch folder. `widgets.props` is replaced only when that build succeeds. A failed build leaves the previous file and the previous program, and prints the build errors. The next `winshell run` or `reload` builds as usual.

### List and remove

`winshell widget list` prints each widget's name, source, ref and commit. It reads an empty file as nothing installed.

`winshell widget remove <name>` drops that source's project reference and its widget items. `<name>` is a widget name or the source. Removing the last source leaves an empty `<Project />`. That file is newer than the program, so the next `winshell run` rebuilds and the shell no longer constructs the removed widget.

### Update

`winshell widget update [<name>]` fetches the recorded ref of every installed source, or of the source `<name>` selects.

- A ref that still names the recorded commit prints `<source> is up to date at <commit>.`
- Otherwise the command prints the old and new commit, the commits `git log --oneline` lists between them, and the widget names that would be added or dropped, then the same warning as install, and asks. `--yes` skips the question. Declining prints `Nothing was updated.` and leaves that source as it is.
- The version check and the staged build are install's: a widget that needs a newer Winshell is refused before the build, and a failed build leaves the previous commit, `widgets.props` and the program.
- Older commits stay in the cache.

## A service

Services hang off `Shell`, created when first read. `shell.Audio`, `shell.Power`, `shell.Apps`, `shell.WindowList`, `shell.Networking`, `shell.Bluetooth`, `shell.Notifications`, `shell.SystemTray`, `shell.Media` and the others are on the [Winshell](reference/winshell.md#shell) page, and each namespace has its own page.

Reading does not ask for permission and does not change anything. Wi-Fi scanning, notification access and Bluetooth pairing ask Windows only when the configuration calls the method, from a user's action. `shell.Session` can ask Windows to lock. Windows' own lock screen takes over.

`shell.NewHotkey()` is a hotkey that works anywhere in Windows. `Sequence` uses `Win`, `Ctrl`, `Alt` and `Shift`.

```csharp
        var hotkey = shell.NewHotkey();
        hotkey.Sequence = "Win+E";
        hotkey.Pressed += (_, _) => { };
```

`shell.Open` opens a Settings page, an app or a URL as Windows would: `ms-settings:sound`, `https://example.com` or an app's path. It takes a URI or a path as a string, not a command line.

```csharp
        var sound = new Button { Content = "Sound" };
        sound.Click += (_, _) => shell.Open("ms-settings:sound");
```

It opens off the shell's thread, so a slow app does not hold up the shell. What Windows can't start, such as a path that is not there, is logged under `shell`, not thrown into the click handler. A scheme no app handles shows Windows' own app picker.

`shell.Log` writes to the instance log, which `winshell log` prints.

### Keep a component current

`UseObservableTree` follows a service: a component renders again when anything that service holds changes, including an item in one of its lists. `UseObservable` follows one service object or one list. Both take an `INotifyPropertyChanged` source, which a service object and an [ObservableList](reference/winshell.md#observablelist) are, and return that source:

```csharp
public sealed class BatteryLabel : Microsoft.UI.Reactor.Core.Component<WidgetContext>
{
    public override Microsoft.UI.Reactor.Core.Element Render()
    {
        var battery = UseObservable(Props.Shell.Power.DisplayDevice);
        var charge = battery.IsPresent ? $"{battery.Percentage:P0}" : "";
        return Microsoft.UI.Reactor.Factories.TextBlock(charge);
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

A class that derives from `Preferences` declares a section. It has one public read-write property per key, and the key is the property name with a lowercase first letter. A key takes a string, a number, `true` or `false`, a list or a table. The property's initializer is its default, and `TomlComment` is the comment written above it. Made with no name, the class holds the configuration's own keys:

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
public sealed class ClockLabel : Microsoft.UI.Reactor.Core.Component
{
    private readonly ShellPreferences _preferences = new();

    public override Microsoft.UI.Reactor.Core.Element Render()
    {
        var preferences = UseObservable(_preferences);
        UseEffect(() => () => _preferences.Dispose(), false);
        return Microsoft.UI.Reactor.Factories.TextBlock(preferences.Clock24 ? "13:00" : "1:00 PM");
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

`winshell doctor` checks the file of the configuration it checks for TOML errors, and names the line. It finds the file by the shell id the configuration has without a `ShellId` in its `ShellOptions`, as [The program](#the-program) describes. For a program that sets one, it checks that default file, not the one the shell reads.

### Data, state and cache

`shell.DataPath`, `shell.StatePath` and `shell.CachePath` are per shell under `%LOCALAPPDATA%\winshell`. State does not survive a reload unless the configuration writes it itself. Every window is replaced.

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
- **A widget that shows an edited type** is made again, with the rest of its widget area, so a change to its constructor shows too. The window, its other areas and every other window stay as they are.
- **Your own control that shows an edited type** anywhere else is made again in its place, so a change to a constructor shows there too. That is the outermost of your controls around it, because its constructor builds everything inside. A Winshell or WinUI control around it, such as a `Grid` you fill in code, stays with everything else it holds. A control in a place a new one can't go, such as a template, stays. The window keeps its place, and every other window keeps its content and its state. A control whose constructor takes arguments stays, and its edited methods still run. Keep state that must survive in a model that control is given.
- **An edit .NET can't apply to running code**, such as a new base class, restarts the shell. That takes as long as a build, and the state of every window goes with it.
- **Code that doesn't compile** shows in a notice on screen, and the last version that did keeps running. The notice goes when the next save compiles. Before the first successful build there is no shell to show it, so the error is in the console.
- **A TOML file with an error** shows in the same notice, and what came from it before stays in use. Without `winshell dev` it goes to the log.

Desktop apps you open from the shell stay open when `winshell dev` builds and starts the shell again, and when `winshell dev` ends, as they do after `winshell kill`.
