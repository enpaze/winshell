# Configuration guide

A configuration is a .NET project that references the Winshell package and builds into one program. This guide builds one up from a single panel. For a whole panel to copy from, see the [Omarchy-style example](https://github.com/enpaze/winshell/tree/main/examples/omarchy-style). The [reference](reference/index.md) lists every type and member. The C# on this page is copied from files the solution builds: [GuideSamples.cs](https://github.com/enpaze/winshell/blob/main/tests/Winshell.Shell.Tests/GuideSamples.cs) and the [message widget](samples/message-widget/Message.xaml.cs). Nothing in GuideSamples runs. Call `Shell.Run` from `Main`, marked `[STAThread]`.

## Create one

You need the [.NET SDK](https://dotnet.microsoft.com/download). `winshell new` lists what it can copy: the starter, which is one panel along the bottom of the screen, and each example, such as the Omarchy-style panel. `winshell new <name>` copies one into your config folder:

```powershell
winshell new
winshell new starter
winshell run -c starter
```

The copy goes to `%APPDATA%\winshell\<name>`, or under `WINSHELL_CONFIG_DIR`. `--path <folder>` puts it somewhere else. A folder that exists and isn't empty is refused. The copy's project references the `Winshell` package of the same release as `winshell`. A release keeps that package in a `packages` folder beside `winshell.exe`, and the copy's `nuget.config` adds that folder as a source. Its shell id comes from its folder, so two copies keep their data apart.

Winshell builds to `bin\shell` in the project folder when a source is newer, then starts that program. `--no-build` runs the program the last build left, and does not build. A failed build does not start the last good build.

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

A downloaded project is refused before any build. Winshell looks for a `Zone.Identifier` stream on the `.csproj` and on `.props` and `.targets` beside it. Unblock the file (right-click > Properties > Unblock, or `Unblock-File`) if you trust it. `git clone` writes no zone, so it is not caught.

A `winshell` and a shell of different releases refuse each other, except `kill`. The message names both releases.

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

`winshell use` builds the configuration first when it needs a build, so the next sign-in can start it. A build that fails changes nothing. The choice is the name in `chosen.txt` in the config folder. Choosing a configuration never changes its shell id, so its data, its state, its widgets and where they are placed stay its own through every switch. `winshell widget` with no `-c` acts on the chosen one.

## The program

`Shell.Run` runs until the shell quits. `ShellId` in `ShellOptions` names the data, state and cache folders. Without it, as in the starter, a configuration in the config folder is named after its folder, and any other after its path.

`DataDir`, `StateDir` and `CacheDir` replace those folders. A relative path is in the project folder, so `data` is `<project>\data`, and a rebuild of `bin\shell` leaves it there. A program a reload built in `bin\shell.next` uses that same folder. A program in neither keeps a relative path next to itself. `%NAME%` expands, and `$BASE` is the folder above `by-shell`, such as `%LOCALAPPDATA%\winshell\data`.

```csharp
Shell.Run<AppShell>(new ShellOptions { ShellId = "MyShell" });
```

`AppShell` is a `ShellConfiguration`. `Start` runs once the shell is up. `Shell.Current` is that shell.

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

`Backdrop` is `WindowBackdrop.Mica`, `Acrylic`, `MicaAlt` or `None`. `Corners` rounds the window. A flyout is a WinUI `Flyout` in the window's content. Winshell closes an open flyout on a window that does not activate.

One panel per screen walks `shell.Screens`. The primary screen is first. The collection changes as monitors are connected and removed.

Reactor is the configuration's choice. Add `Microsoft.UI.Reactor` in the project and set `new ReactorHostControl(component)` as a window's content. Winshell never references it.

## Widgets

A [WidgetArea](reference/winshell.md#widgetarea) can go anywhere in a window. List widgets in your configuration's project, next to their project or package references:

```xml
<ItemGroup>
  <WinshellWidget Include="message" Type="MyShell.MessageWidget" />
</ItemGroup>
```

`Include` is the case-sensitive placement name; `Type` is the C# type name of a WinUI element with a constructor that takes a `WidgetContext`. The build generates direct factory registrations that run before `Main`, without reflection. A missing type, a type that is not a C# type name, and a duplicate name fail the build, and the error names the widget. Names are escaped for C# automatically. You can still call `Widgets.Register` directly for factories that need custom construction.

### Installed widgets

Installed widgets live in `widgets.props`, beside the project file. When that file exists, the build imports it, so `dotnet build`, Visual Studio and `winshell run` include the same widgets. The file is the only record of an install. `winshell widget install` writes it, `winshell widget list` reads it, `winshell widget update` moves an installed source to a newer commit, and `winshell widget remove` rewrites it. An empty `<Project />` means nothing is installed. The template ignores it, and so does the Omarchy example, so an install stays out of the shell's source.

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

`Include` on the project reference is the project inside a clone of one commit. `WinshellSource` is the repository, `WinshellRef` is the branch, tag or commit that was asked for, and `WinshellCommit` is the commit that was cloned. `UndefineProperties` names `WinshellWidgetsFile`, so that global property does not flow into the referenced project and the library does not import this file and reference itself. Each `WinshellWidget` item follows the reference it came from.

`winshell widget install github.com/someone/clock-widget@v1.2` resolves that ref to a commit and clones it under `%LOCALAPPDATA%\winshell\widgets\someone\clock-widget\<commit>`, or under `WINSHELL_LOCAL_DIR` when that is set. A `file://` URL or a folder is cloned the same way, under `widgets\local`. With no `--project`, the repository must contain one project that lists `WinshellWidget` items; otherwise the command names the projects and installs nothing. It prints the repository, the commit and the widget names, warns that building runs the repository's code and MSBuild targets in a program that starts at sign-in, and asks. `--yes` skips the question. Declining changes nothing.

A widget whose `Winshell` package reference needs a newer version than the configuration's is refused before that build, and the message names both versions. A widget name already installed from another source is refused, and so is a source that is already installed. Installing builds the configuration against a staged copy, with `-p:WinshellWidgetsFile` set to that copy's absolute path, into a scratch folder. `widgets.props` is replaced only when that build succeeds. A failed build leaves the previous file and the previous program, and prints the build errors. The next `winshell run` or `reload` builds as usual.

`winshell widget list` prints each widget's name, source, ref and commit. `winshell widget remove <name>` drops that source's project reference and its widget items. `<name>` is a widget name or the source. Removing the last source leaves an empty `<Project />`. That file is newer than the program, so the next `winshell run` rebuilds and the shell no longer constructs the removed widget. `winshell widget list` reads an empty file as nothing installed. Install, list, remove and update take `-c` and `-p`. Install and update need `git` and the .NET SDK, and say so when either is missing.

`winshell widget update [<name>]` fetches the recorded ref of every installed source, or of the source `<name>` selects. A ref that still names the recorded commit prints `<source> is up to date at <commit>.` Otherwise the command prints the old and new commit, the commits `git log --oneline` lists between them, and the widget names that would be added or dropped, then the same warning as install, and asks. `--yes` skips the question. Declining prints `Nothing was updated.` and leaves that source as it is. The version check and the staged build are install's: a widget that needs a newer Winshell is refused before the build, and a failed build leaves the previous commit, `widgets.props` and the program. Older commits stay in the cache.

Set `WinshellWidgetsFile` to an absolute path to import a different file: `dotnet build -p:WinshellWidgetsFile=...`. A relative path is resolved from the folder that contains `Winshell.props`, not from the project. The default is `widgets.props` in the project directory.

The build writes registration code only for an executable, an `OutputType` of `Exe` or `WinExe`. A widget library can list `WinshellWidget` items so `dotnet msbuild -getItem:WinshellWidget` can read them, and that library does not register the widgets. The configuration that references the library does. Registering one name from both fails when the shell starts.

For example, define this widget in your project's `MyShell` namespace:

```csharp
public sealed partial class MessageWidget : UserControl
{
    public MessageWidget(WidgetContext context)
    {
        Content = new TextBlock { Text = (string)context.Settings["text"] };
    }
}
```

Each placement creates a fresh WinUI element with its own settings, the area's orientation and the running `Shell` in its `WidgetContext`. Several placements may use one widget:

```csharp
        var area = new WidgetArea
        {
            Name = "end",
            Orientation = Orientation.Horizontal,
            Defaults =
            [
                new("message", new Dictionary<string, object> { ["text"] = "Hello" }),
                new("message", new Dictionary<string, object> { ["text"] = "World" })
            ]
        };
        var panel = new PanelWindow { Height = 48, Content = area, Visible = true };
        panel.Closed += area.Dispose;
```

Assigning `Defaults` snapshots the list and replaces its widgets unless the user placement file overrides the area. Changing `Orientation` also recreates them. Both dispose every widget implementing `IDisposable`. The example treats a user closing the panel as permanent: `Closed` normally only hides a window. When your code tears the panel down with `panel.Dispose()`, explicitly call `area.Dispose()` too. Removing or reparenting the area does not dispose widgets. Treat placement settings, including nested tables, as read-only.

The [shell style](#style) styles the plain controls in an area. Use `{ThemeResource}` for area-sensitive widget resources: `{StaticResource}` resolves at construction, before the area owns the widget. Assign `context.StylePopup(content)` to a `PopupWindow.Content` to load a fresh dictionary of the area's style and follow its resolved theme, style, font, font size and foreground until the area removes the widget or is disposed; the widget still owns its popup windows.

An unknown widget or a factory that throws shows a placeholder with its name and reason while the other placements keep running. The exception and its stack trace are logged under `widgets` in the shell's log (`winshell log`). Throw `UnsupportedWidgetException` with a reason when a widget cannot run in its placement, such as a vertical area. Exceptions after creation follow the shell's normal crash supervision.

### User placement file

Set an area's inherited WinUI `Name` to let the user replace its placements. Names are case-sensitive; areas with the same name use the same list. An area with no name always uses its defaults.

The file is `%APPDATA%\winshell\widgets\<shell id>.toml`, or `<WINSHELL_CONFIG_DIR>\widgets\<shell id>.toml` when that variable is set. It lives outside the shell project and survives shell updates. When it is missing, Winshell writes the current named areas' defaults as comments. Uncomment a whole list and edit it:

```toml
[[end]]
widget = "message"
text = "Hello from the placement file"

[[end]]
widget = "message"
text = "A second placement"
```

Each array of tables replaces that area's **whole** default list, in order. `widget` names the registered factory; the other keys become its `WidgetContext.Settings`, including nested tables and arrays. Use `end = []` before any `[[area]]` headers to leave an area empty. Remove the entry to return to the author's defaults. An area absent from the file keeps its defaults.

Saving applies changes live. Each changed area disposes its old widgets and creates the new list; other areas keep their widgets. A syntax error or a list without string `widget` names keeps the last valid lists and logs the file location under `widgets`. A name that matches no area stays in the file and produces a warning naming it. Winshell does not migrate or merge user lists into new defaults.

### Style

The style is how every widget area and styled popup looks: its colors, font and slot size. With none set, the built-in style follows Windows: its text and background colors in light, in dark and in a contrast theme, Segoe UI and Segoe Fluent Icons. Set `shell.Style` once to a `ResourceDictionary` file to give the whole shell another look:

```csharp
        shell.Style = new Uri("ms-appx:///TokyoNight.xaml");
```

A new value reaches what is already on screen, and `null` returns to the built-in style. An area's own `Style` wins over the shell's, and `null` there follows the shell. The file is a XAML page of your project, as `ms-appx:///` names it.

A style defines these keys, which a bar, a widget and its popups read with `{ThemeResource}`:

| Key | Type | What it is | Built-in |
| --- | --- | --- | --- |
| `ShellBackgroundBrush` | `Brush` | Behind a bar or a popup | Windows' solid background |
| `ShellForegroundBrush` | `Brush` | Text and glyphs | Windows' primary text color |
| `ShellAlertBrush` | `Brush` | Something that needs attention, such as a low battery | Windows' critical color |
| `ShellFontFamily` | `FontFamily` | Text | Segoe UI Variable Text, then Segoe UI |
| `ShellFontSize` | `Double` | Text | 14 |
| `ShellSlotSize` | `Double` | One button, and the thickness of a bar | 32 |

It also defines implicit styles, with no `x:Key`, for the plain controls a widget is made of. They apply inside a widget area and in a popup styled with `context.StylePopup`, so a widget that is a `Button` holding a `FontIcon` looks right with no lookup:

- `Button`: flat, at least one slot wide and high, its content centered, in the style's foreground, font and font size.
- `TextBlock`: the style's foreground, font and font size, centered vertically.
- `FontIcon`: the style's icon font. Its color comes from what holds it, so a button's pressed and disabled colors reach the glyph.
- `ToolTip`: the style's background, foreground and font. WinUI looks up a tooltip's style only in the app's resources, because a tooltip opens outside the area, so this entry does not yet reach a tooltip a widget sets with `ToolTipService`.

A style that leaves out a key fails where something reads it. [BuiltInStyle.xaml](https://github.com/enpaze/winshell/blob/main/src/Winshell.Shell/BuiltInStyle.xaml) is the built-in style, and a start for your own:

```xml
<ResourceDictionary
  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="ShellBackgroundBrush" Color="#1a1b26" />
  <SolidColorBrush x:Key="ShellForegroundBrush" Color="#a9b1d6" />
  <SolidColorBrush x:Key="ShellAlertBrush" Color="#f7768e" />
  <FontFamily x:Key="ShellFontFamily">Consolas</FontFamily>
  <x:Double x:Key="ShellFontSize">12</x:Double>
  <x:Double x:Key="ShellSlotSize">26</x:Double>

  <Style TargetType="Button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="Padding" Value="0" />
    <Setter Property="MinWidth" Value="{StaticResource ShellSlotSize}" />
    <Setter Property="MinHeight" Value="{StaticResource ShellSlotSize}" />
    <Setter Property="VerticalAlignment" Value="Center" />
    <Setter Property="Foreground" Value="{StaticResource ShellForegroundBrush}" />
    <Setter Property="FontFamily" Value="{StaticResource ShellFontFamily}" />
    <Setter Property="FontSize" Value="{StaticResource ShellFontSize}" />
  </Style>
  <Style TargetType="TextBlock">
    <Setter Property="Foreground" Value="{StaticResource ShellForegroundBrush}" />
    <Setter Property="FontFamily" Value="{StaticResource ShellFontFamily}" />
    <Setter Property="FontSize" Value="{StaticResource ShellFontSize}" />
    <Setter Property="VerticalAlignment" Value="Center" />
  </Style>
  <Style TargetType="FontIcon">
    <Setter Property="FontFamily" Value="Segoe Fluent Icons,Segoe MDL2 Assets" />
    <Setter Property="FontSize" Value="14" />
  </Style>
  <Style TargetType="ToolTip">
    <Setter Property="Background" Value="{StaticResource ShellBackgroundBrush}" />
    <Setter Property="Foreground" Value="{StaticResource ShellForegroundBrush}" />
    <Setter Property="FontFamily" Value="{StaticResource ShellFontFamily}" />
  </Style>
</ResourceDictionary>
```

A style with one look whatever the theme uses plain keys, as this one does. A style that changes with the theme puts its brushes in `ResourceDictionary.ThemeDictionaries`, under `Default` for dark, `Light` and `HighContrast`, as the built-in style does.

## Write a widget

A widget other people install is a WinUI class library. [samples/message-widget](samples/message-widget) is one, and the solution builds it: a line of text, with one `text` setting. It is a sample for this guide, not a widget Winshell ships.

The project lists each widget. `Include` is the placement name and `Type` is the C# type, as in a configuration. `winshell widget install` reads those items with `dotnet msbuild -getItem:WinshellWidget`. The library does not register them. Registration is generated only for an executable, so the configuration that references the library is the one that constructs the widget.

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
  <ItemGroup>
    <WinshellWidget Include="message" Type="MessageWidget.Message" />
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

[Message.xaml](samples/message-widget/Message.xaml) draws the line. Its foreground is `{ThemeResource TextFillColorPrimaryBrush}`, so it follows the area's theme, including a change of theme. `{StaticResource}` resolves at construction, before the area owns the widget, and does not follow a later change.

The constructor takes a [WidgetContext](reference/winshell.md#widgetcontext):

```csharp
public sealed partial class Message : IDisposable
{
    private readonly WidgetContext _context;
    private PopupWindow? _popup;
    private bool _disposed;

    public Message(WidgetContext context)
    {
        var text = TextOf(context);
        if (context.Orientation == Orientation.Vertical)
            throw new UnsupportedWidgetException("The message widget runs in a horizontal area.");

        InitializeComponent();
        _context = context;
        Label.Text = text + " ·";
        Label.PointerPressed += (_, _) => ShowPopup();
        Loaded += (_, _) => ShowScreen();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _popup?.Dispose();
        _popup = null;
    }

    private static string TextOf(WidgetContext context)
    {
        if (context.Settings.TryGetValue("text", out var value) && value is string text)
            return text;
        throw new UnsupportedWidgetException("text must be a string.");
    }

    private void ShowPopup()
    {
        if (_disposed)
            return;
        _popup?.Dispose();
        var body = new TextBlock { Text = Label.Text, Margin = new Thickness(12) };
        _popup = new PopupWindow { Content = _context.StylePopup(body), Visible = true };
    }

    private void ShowScreen()
    {
        if (_disposed)
            return;
        var screen = ShellWindow.Of(this)?.Screen;
        ToolTipService.SetToolTip(Label, screen is null ? "primary screen" : screen.Name);
    }
}
```

`text` is the placement's setting. The line is that string followed by a middle dot. A value that is not a string throws [UnsupportedWidgetException](reference/winshell.md#unsupportedwidgetexception), and so does a vertical area. The area shows the widget's name and that reason, and the other placements keep running.

[StylePopup](reference/winshell.md#widgetcontext) wraps content for a [PopupWindow](reference/winshell.md#popupwindow). Assign the result to the popup's `Content`. The wrapper loads the area's style and follows its theme, font, font size and foreground. The widget still owns the popup: this one disposes the previous popup, and disposes it again with the widget.

[ShellWindow.Of](reference/winshell.md#shellwindow) is null in the constructor. The widget is created before its area is in a window, so the screen is read from `Loaded`. Null is the primary screen.

## A service

Services hang off `Shell`, created when first read. `shell.Audio`, `shell.Power`, `shell.Apps`, `shell.WindowList`, `shell.Networking`, `shell.Bluetooth`, `shell.Notifications`, `shell.SystemTray`, `shell.Media` and the others are on the [Winshell](reference/winshell.md#shell) page, and each namespace has its own page.

Reading does not ask for permission and does not change anything. Wi-Fi scanning, notification access and Bluetooth pairing ask Windows only when the configuration calls the method, from a user's action. `shell.Session` can ask Windows to lock. Windows' own lock screen takes over.

`shell.NewHotkey()` is a hotkey that works anywhere in Windows. `Sequence` uses `Win`, `Ctrl`, `Alt` and `Shift`.

```csharp
        var hotkey = shell.NewHotkey();
        hotkey.Sequence = "Win+E";
        hotkey.Pressed += (_, _) => { };
```

`shell.Log` writes to the instance log, which `winshell log` prints.

### Keep a control current

`Watch` keeps a control showing what the services hold. Winshell runs the block, records each service property and list it reads, and runs it again when one of them changes:

```csharp
        var label = new TextBlock();
        label.Watch(() =>
        {
            var battery = shell.Power.DisplayDevice;
            label.Text = battery.IsPresent ? $"{battery.Percentage:P0}" : "";
        });
```

Several changes in one turn run the block once, on the control's thread, and a change the block makes itself does not run it again. The watch pauses while the control is unloaded, as the content of a hidden popup is, and runs again when it loads. It stops when the control's window is disposed, so there is nothing to dispose. A block that throws is logged under `watch`, and the next change to what it read before the throw runs it again.

A service list, such as `shell.Power.Devices`, is an [ObservableList](reference/winshell.md#observablelist): going through it, counting it or indexing it is a read. Only its service changes it. A class of your own that derives from `ObservableObject` reports a read from each getter, as in `get => Read(field);`.

## IPC

`shell.Ipc` is what `winshell ipc` calls. The handler's own types say what it takes and returns. `string`, `int`, `bool`, `double`, `float` and `Windows.UI.Color` cross the pipe. Nothing is evaluated.

```csharp
        shell.Ipc.Handle("notes", "hello", (string name) => "hi " + name);
```

`winshell ipc call notes hello Ada` prints `hi Ada`. `Property` and `Signal` are on [ShellIpc](reference/winshell.md#shellipc). Dispose the registration to take it away.

`winshell ipc call notice dismiss` closes Winshell's own notice. That call is not a target you register, so `ipc show` does not list it.

## Preferences

Preferences stay TOML. A subclass of `TomlFile` has one public read-write property per key. The key is the property name with a lowercase first letter. `Set` writes the field and the file. A comment on a key the file does not have yet comes from `TomlComment`.

```csharp
public sealed class ShellPreferences(string path, IEventLoop loop) : TomlFile(path, loop)
{
    [TomlComment("\"system\" follows Windows; or \"light\" or \"dark\".")]
    public string ColorScheme
    {
        get;
        set => Set(ref field, value);
    } = "system";
}
```

Pass `Path.Combine(shell.ConfigRoot, "preferences.toml")` and `shell.Loop`. The object follows hand edits. Unknown keys and comments stay. An unknown key that differs from a key the class reads only in case, by a letter, or by two in a longer key is logged with its line and that key. A file that does not parse is not written. A file past 1 MiB, or nested past 64 levels, is not read. `Error` is `path[line:column]: message`, or empty.

`shell.DataPath`, `shell.StatePath` and `shell.CachePath` are per shell under `%LOCALAPPDATA%\winshell`. State does not survive a reload unless the configuration writes it itself. Every window is replaced.

## Reload

```csharp
        if (!shell.Reload())
            shell.Log.Warn("shell", "Reload did not start.");
```

That starts this program again with `--after` and quits. It does not build, and the new process runs with no supervisor. `winshell reload` builds first. On failure the running instance stays and a notice shows. On success the new instance takes over, and under `winshell run` it stays supervised: `winshell run` starts it after this one quits. There is no file watcher. [`winshell dev`](#develop-a-shell) applies each edit as you save.

`winshell run --no-build` skips the build. The sign-in entry uses it, so signing in does not need the SDK and does not run a broken edit.

## Develop a shell

```powershell
winshell dev --path .
```

`winshell dev` builds your project, starts it, and applies each save to the running shell until you press Ctrl+C, which closes that shell and stops `dotnet watch`, as closing the terminal does. It needs the .NET SDK, and it runs a Debug build. With no project it prints `winshell dev needs a configuration project. Run winshell new to make one.` and exits with 1.

- **A C# edit** reaches the running shell in about a second. The first one takes a little longer, while the compiler warms up. An edited method runs its new code at its next call.
- **A window showing an edited type** gets a new one of its content, so a change to a constructor shows too. The window keeps its place, and every other window keeps its content and its state. A view whose constructor takes arguments stays, and its edited methods still run. Keep state that must survive in a model the view is given.
- **A XAML edit** goes into the panels of that class as you save, in a moment and with no build. The control stays the same object, so its C# fields keep their values, and its `x:Name` fields and event handlers point at the new elements. `{x:Bind}`, `x:Load` and `x:Phase` are the compiler's, and so is a file with no `x:Class`, so saving one of those builds and starts the shell again. A panel with no window open yet shows the new markup when it opens.
- **An edit .NET can't apply to running code**, such as a new base class, restarts the shell. That takes as long as a build, and the state of every window goes with it.
- **Code that doesn't compile** shows in a notice on screen, and the last version that did keeps running. The notice goes when the next save compiles. Before the first successful build there is no shell to show it, so the error is in the console.
- **A TOML or XAML file with an error** shows in the same notice, and what came from it before stays in use. Without `winshell dev` it goes to the log.

Desktop apps you open from the shell stay open when `winshell dev` builds and starts the shell again, and when `winshell dev` ends, as they do after `winshell kill`.
