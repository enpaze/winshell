# Omarchy-style panel

A Winshell configuration that draws the bar of [Omarchy](https://github.com/omacom/omarchy) v4.5.0 as a panel, in Omarchy's Tokyo Night style. Copy from it: it shows a panel per screen, on whichever edge you choose, Winland's workspaces, the tray, and the status of Bluetooth, the network, audio, the screen and the battery, each read from a Winshell service.

![The panel: the Windows glyph and workspaces 1 to 5 on the left, the clock in the middle, the tray chevron, tray icons and status glyphs on the right](screenshot.png)

## What it shows

One panel on every screen, along the top unless the bar file says another edge. Top and bottom are `PanelHeight` thick; left and right are one `SlotWidth` wide. Both are in [PanelStyles.xaml](PanelStyles.xaml). The panel reserves that edge, and Explorer's taskbar is hidden while the shell runs; Winshell shows the taskbar again when the shell quits. The colors are Tokyo Night's, defined in [TokyoNight.xaml](TokyoNight.xaml), with an alert color for a network that can't reach the internet, whatever the Windows theme is.

The bar is three widget areas. The default layout names them `start`, `center` and `end`. A layout called `side` names them `side.start`, `side.center` and `side.end`, so each layout has its own lists in the placement file. On the left or right edge the areas run vertically. Each glyph is its own widget, so a placement can reorder or repeat one without taking the others with it. With no placement file the areas use the defaults below and a top bar looks as it does in the picture.

- **Start:** the `windows` glyph, then `workspaces` for Winland's workspaces on that widget's own screen: 1 to 5 always, and any other that exists. An empty one is dimmed, the shown one is the tiling glyph, and a click shows it. Without Winland running there are no workspaces. On a vertical bar the buttons stack. A widget is created before its area is in a window, so these read the screen from `ShellWindow.Of` once they have loaded.
- **Center:** the `clock`, the day and time, such as `Saturday 20:25`, in your Windows regional format. A `timezone` setting names another zone, such as `UTC`; without one the clock is local, and two clocks can show two zones. On a vertical bar the hours sit above the minutes, such as `20` over `25`, and the tooltip stays the full date.
- **End:** `tray` on the primary screen only. Hovering its chevron slides out the icons Windows keeps in its overflow: out to the side on a top or bottom bar, and downwards where the icons stack. Then `bluetooth`, `network`, `audio`, `screen` and `battery`; each opens its Windows Settings page when clicked. The battery is left out on a PC without one, and Bluetooth when there is no adapter.

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
| [AppShell.cs](AppShell.cs) | Hides Explorer's taskbar and places a panel on each screen, from the bar file. |
| [BarSettings.cs](BarSettings.cs) | The bar file: the edge, and which monitors use which layout. |
| [PanelView.xaml](PanelView.xaml) | Lays the three areas across the bar and gives each Tokyo Night as its theme. |
| [PanelView.xaml.cs](PanelView.xaml.cs) | Names the areas for the layout, gives them the default placements, and disposes them with the panel. |
| [PanelStyles.xaml](PanelStyles.xaml) | Merges Tokyo Night, names the sizes, and defines the button, text and tooltip styles. |
| [Widgets/](Widgets) | One class per widget. `StatusWidget` is the shared status button; the others are the glyph, the workspaces, the clock and the tray. |
| [TokyoNight.xaml](TokyoNight.xaml) | The areas' theme: the bar's brushes and font, and the WinUI text, accent, control-fill and font keys a stock control uses. |
| [Glyphs.cs](Glyphs.cs) | Names each glyph with its Nerd Fonts name and chooses status glyphs. |
| [Watcher.cs](Watcher.cs) | Renders a widget again when a service object or list it read changes; [#1706](https://github.com/enpaze/winshell/issues/1706) will replace it with automatic tracking. |
| [OmarchyStyle.csproj](OmarchyStyle.csproj) | References Winshell, registers each widget with a `WinshellWidget` item, and copies the font files beside the program. |
| [Fonts/README.md](Fonts/README.md) | Records the bundled font's source and licences. |

## Change it

- **Colors and font:** replace [TokyoNight.xaml](TokyoNight.xaml), the areas' `Theme`. A widget applies `PanelButton` or `PanelText` once it has loaded. Those styles use `{ThemeResource}`, which follows the area. The widget's own markup is loaded before the area owns it, so a resource lookup there cannot see the area yet. The dictionary also sets WinUI's text, accent, control-fill and `ContentControlThemeFontFamily` keys, in its theme dictionaries, so a widget written only with stock controls is Tokyo Night too.
- **Size and controls:** edit [PanelStyles.xaml](PanelStyles.xaml) for the button template, tooltip styles and named sizes: `PanelHeight` for a top or bottom bar and the edge it reserves, `SlotWidth` for a left or right bar and for status and tray buttons, `IconSize` for tray icons, `PanelFontSize` for text and glyphs, `WorkspaceWidth` for workspace buttons, and `PanelInset` for the edges. The C# reads these resources too.
- **Move the bar:** edit `%APPDATA%\winshell\bar\<shell id>.toml`, or `<WINSHELL_CONFIG_DIR>\bar\<shell id>.toml` when that variable is set. `<shell id>` is the placement file's name without `.toml`. The file is not the placement file: a key there that names no area is a warning, so the edge and the monitors stay here. With no file, every screen gets a top bar and the areas `start`, `center` and `end`. Saving applies live. `winshell log` prints a `bar` line per screen, `manufacturer / model / serial on the <edge>`, which is the identity to copy. Match those three, never `\\.\DISPLAYn`, which Windows renumbers. A row for a monitor that is not connected stays in the file and is not shown on another screen.

  ```toml
  edge = "top"

  [[monitor]]
  manufacturer = "GSM"
  model = "LG ULTRAGEAR"
  serial = "ABC123"
  edge = "left"
  layout = "side"
  ```

  `edge` is `top`, `bottom`, `left` or `right`. A monitor with no row uses that edge and the default areas. A row matches when manufacturer, model and serial all equal the monitor's, including an empty serial, and the first match wins. The row's `edge` overrides the file's; omit it to keep the file's. `layout` omitted, or `""`, uses `start`, `center` and `end`. A name such as `side` uses `side.start`, `side.center` and `side.end`. A name is a letter or `_`, then letters, digits, `_` and `-`.

- **Rearrange the bar:** edit the placement file, `%APPDATA%\winshell\widgets\<shell id>.toml` (or under `WINSHELL_CONFIG_DIR`). Saving replaces an area's whole list, live. A layout's areas are quoted because of the dot: `[["side.start"]]`. This puts a UTC clock beside the local one and reverses the end of the default layout:

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
