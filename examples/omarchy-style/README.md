# Omarchy-style panel

A Winshell configuration that draws the top bar of [Omarchy](https://github.com/omacom/omarchy) v4.5.0 as a panel, in Omarchy's Tokyo Night style. Copy from it: it shows a panel per screen, Winland's workspaces, the tray, and the status of Bluetooth, the network, audio, the screen and the battery, each read from a Winshell service.

![The panel: the Windows glyph and workspaces 1 to 5 on the left, the clock in the middle, the tray chevron, tray icons and status glyphs on the right](screenshot.png)

## What it shows

One panel along the top of every screen, sized by `PanelHeight` in [PanelStyles.xaml](PanelStyles.xaml). It reserves that edge, and Explorer's taskbar is hidden while the shell runs; Winshell shows the taskbar again when the shell quits. The colors are Tokyo Night's, defined in [TokyoNight.xaml](TokyoNight.xaml), with an alert color for a network that can't reach the internet, whatever the Windows theme is.

The bar is three widget areas, named `start`, `center` and `end`. Each glyph is its own widget, so a placement can reorder or repeat one without taking the others with it. With no placement file the areas use the defaults below and the bar looks as it does in the picture.

- **Start:** the `windows` glyph, then `workspaces` for Winland's workspaces on that widget's own screen: 1 to 5 always, and any other that exists. An empty one is dimmed, the shown one is the tiling glyph, and a click shows it. Without Winland running there are no workspaces. A widget is created before its area is in a window, so these read the screen from `ShellWindow.Of` once they have loaded.
- **Center:** the `clock`, the day and time, such as `Saturday 20:25`, in your Windows regional format. A `timezone` setting names another zone, such as `UTC`; without one the clock is local, and two clocks can show two zones.
- **End:** `tray` on the primary screen only. Hovering its chevron slides out the icons Windows keeps in its overflow. Then `bluetooth`, `network`, `audio`, `screen` and `battery`; each opens its Windows Settings page when clicked. The battery is left out on a PC without one, and Bluetooth when there is no adapter.

| Widget | Area | Settings |
| --- | --- | --- |
| `windows` | start | none |
| `workspaces` | start | none |
| `clock` | center | `timezone`, optional: a Windows time zone id |
| `tray` | end | none |
| `bluetooth` | end | none |
| `network` | end | none |
| `audio` | end | none |
| `screen` | end | none |
| `battery` | end | none |

Hover the clock, a tray icon or a status glyph for what it shows.

## Run it

<!-- winshell new leaves this section out -->

From a clone of Winshell, with `winshell` [built from that clone](../../docs/development.md#build-from-source):

```powershell
winshell run --path examples/omarchy-style
```

The project references Winshell's source, so the repo's own check builds it.

## How it's built

| File | Job |
| --- | --- |
| [Program.cs](Program.cs) | Starts the shell with this configuration's identity. |
| [AppShell.cs](AppShell.cs) | Hides Explorer's taskbar and places a panel on each screen. |
| [PanelView.xaml](PanelView.xaml) | Lays the `start`, `center` and `end` areas across the bar and gives each Tokyo Night as its theme. |
| [PanelView.xaml.cs](PanelView.xaml.cs) | Names the areas, gives them the default placements, and disposes them with the panel. |
| [PanelStyles.xaml](PanelStyles.xaml) | Merges Tokyo Night, names the sizes, and defines the button, text and tooltip styles. |
| [Widgets/](Widgets) | One class per widget. `StatusWidget` is the shared status button; the others are the glyph, the workspaces, the clock and the tray. |
| [TokyoNight.xaml](TokyoNight.xaml) | The areas' theme: the bar's brushes and font, and the WinUI text, accent, control-fill and font keys a stock control uses. |
| [Glyphs.cs](Glyphs.cs) | Names each glyph with its Nerd Fonts name and chooses status glyphs. |
| [Watcher.cs](Watcher.cs) | Renders a widget again when a service object or list it read changes; [#1706](https://github.com/enpaze/winshell/issues/1706) will replace it with automatic tracking. |
| [OmarchyStyle.csproj](OmarchyStyle.csproj) | References Winshell, registers each widget with a `WinshellWidget` item, and copies the font files beside the program. |
| [Fonts/README.md](Fonts/README.md) | Records the bundled font's source and licences. |

## Change it

- **Colors and font:** replace [TokyoNight.xaml](TokyoNight.xaml), the areas' `Theme`. A widget applies `PanelButton` or `PanelText` once it has loaded. Those styles use `{ThemeResource}`, which follows the area. The widget's own markup is loaded before the area owns it, so a resource lookup there cannot see the area yet. The dictionary also sets WinUI's text, accent, control-fill and `ContentControlThemeFontFamily` keys, in its theme dictionaries, so a widget written only with stock controls is Tokyo Night too.
- **Size and controls:** edit [PanelStyles.xaml](PanelStyles.xaml) for the button template, tooltip styles and named sizes: `PanelHeight` for the panel and its reserved edge, `SlotWidth` for status and tray buttons, `IconSize` for tray icons, `PanelFontSize` for text and glyphs, `WorkspaceWidth` for workspace buttons, and `PanelInset` for the edges. The C# reads these resources too.
- **Rearrange the bar:** edit the placement file, `%APPDATA%\winshell\widgets\<shell id>.toml` (or under `WINSHELL_CONFIG_DIR`). Saving replaces an area's whole list, live. This puts a UTC clock beside the local one and reverses the end of the bar:

  ```toml
  [[center]]
  widget = "clock"

  [[center]]
  widget = "clock"
  timezone = "UTC"

  [[end]]
  widget = "battery"

  [[end]]
  widget = "screen"

  [[end]]
  widget = "audio"

  [[end]]
  widget = "network"

  [[end]]
  widget = "bluetooth"

  [[end]]
  widget = "tray"
  ```

- **Live-edit the layout:** run `winshell dev --path examples/omarchy-style` and save [PanelView.xaml](PanelView.xaml). Its markup avoids `x:Bind` so live editing keeps working without a rebuild or restart. Edits to the resource dictionaries rebuild and restart the shell; see [Develop a shell](../../docs/configuration.md#develop-a-shell).
- **Add a widget:** copy [Widgets/AudioWidget.cs](Widgets/AudioWidget.cs), give the class its Settings page, and add a `WinshellWidget` item in [OmarchyStyle.csproj](OmarchyStyle.csproj) whose `Include` is the placement name. Put that name in an area's defaults in [PanelView.xaml.cs](PanelView.xaml.cs).
- **Keep the taskbar:** delete `shell.ExplorerTaskbar.Hidden = true;` in [AppShell.cs](AppShell.cs).

## Use it as your own configuration

<!-- winshell new leaves this section out -->

Copy the folder out of the repo, then reference the `Winshell` package instead of the source. In `OmarchyStyle.csproj`, delete the `<Import>` of `Winshell.props` and replace the `<ProjectReference>` with:

```xml
<PackageReference Include="Winshell" Version="<version>" />
```

The package is not published yet, so pack it from the same clone and point NuGet at it, as [Create one](../../docs/configuration.md#create-one) in the configuration guide shows. `<version>` is the release in the package's name.

## Credits

- The look, the sizes, the glyph choices and the colors come from [Omarchy v4.5.0](https://github.com/omacom/omarchy/tree/v4.5.0) by David Heinemeier Hansson, under the MIT licence. The rules for which glyph each status shows follow Omarchy's widgets; no Omarchy file, logo or font is included, and the Windows glyph stands where Omarchy's logo is.
- [JetBrainsMono Nerd Font](https://github.com/ryanoasis/nerd-fonts/releases/tag/v3.5.1), from Nerd Fonts v3.5.1, ships in `Fonts/`: the Regular weight only. JetBrains Mono is under the SIL Open Font License 1.1, in [Fonts/OFL.txt](Fonts/OFL.txt). Its icon sets keep their own licences, which [Fonts/README.md](Fonts/README.md) lists.
