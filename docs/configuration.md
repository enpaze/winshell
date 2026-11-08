# Configuration guide

A configuration is a .NET project that references the Winshell package and builds into one program. This guide builds one up from a single panel. For a whole panel to copy from, see the [Omarchy-style example](https://github.com/enpaze/winshell/tree/main/examples/omarchy-style). The [reference](reference/index.md) lists every type and member. The C# on this page is copied from [tests/Winshell.Shell.Tests/GuideSamples.cs](https://github.com/enpaze/winshell/blob/main/tests/Winshell.Shell.Tests/GuideSamples.cs), which the solution builds. Nothing in that file runs. Call `Shell.Run` from `Main`, marked `[STAThread]`.

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
| `winshell run` | The project in `%APPDATA%\winshell`; else `%APPDATA%\winshell\default`; else the one subfolder of `%APPDATA%\winshell` that has a project. With several, Winshell asks you to pick one with `--config`. With none, nothing runs. |

`WINSHELL_CONFIG_DIR` replaces `%APPDATA%\winshell` in this table.

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

Installed widgets live in `widgets.props`, beside the project file. When that file exists, the build imports it, so `dotnet build`, Visual Studio and `winshell run` include the same widgets. The file is ordinary MSBuild:

```xml
<Project>
  <ItemGroup>
    <ProjectReference Include="..\clock-widget\ClockWidget.csproj" />
    <WinshellWidget Include="clock" Type="ClockWidget.Clock" />
  </ItemGroup>
</Project>
```

`winshell widget install`, `winshell widget remove` and `winshell widget update` write this file. The template ignores it, and so does the Omarchy example, so an install stays out of the shell's source.

Set `WinshellWidgetsFile` to an absolute path to import a different file: `dotnet build -p:WinshellWidgetsFile=...`. A relative path is resolved from the folder that contains `Winshell.props`, not from the project. The default is `widgets.props` in the project directory. Installing builds against a staged copy this way and replaces `widgets.props` only when that build succeeds.

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

Set `Theme` to a `ResourceDictionary` source URI to merge it into the area's resources. Font and foreground inherit through the WinUI tree. Use `{ThemeResource}` for area-sensitive widget resources: `{StaticResource}` resolves at construction, before the area owns the widget. Assign `context.StylePopup(content)` to a `PopupWindow.Content` to load a fresh dictionary from the area's `Theme` URI and follow its resolved theme, font, font size and foreground until the area removes the widget or is disposed; the widget still owns its popup windows.

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
