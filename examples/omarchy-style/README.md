# Omarchy-style panel

A Winshell configuration that draws the bar of [Omarchy](https://github.com/omacom/omarchy) v4.5.0 as a panel, in Omarchy's Tokyo Night style. Copy from it: it shows a bar on every screen, on whichever edge you choose, Winland's workspaces, the tray, and the status of Bluetooth, the network, audio, the screen and the battery, each read from a Winshell service.

![The panel: the Windows glyph and workspaces 1 to 5 on the left, the clock in the middle, the tray chevron, tray icons and status glyphs on the right](screenshot.png)

## What it shows

A bar on every screen, along the top unless the preferences file says another edge. It is one slot of the style thick, 26 pixels: the height of a top or bottom bar and the width of a side one. The bar reserves its edge, and Explorer's taskbar is hidden while the shell runs; Winshell shows the taskbar again when the shell quits. The colors are Tokyo Night's, with an alert color for a network that can't reach the internet, whatever the Windows theme is.

The bar has three areas: start, center and end. On a left or right edge they run from top to bottom. Each glyph is its own widget, so a placement can reorder or repeat one without taking the others with it. With no lists in the preferences file the areas use the ones in [AppShell.cs](AppShell.cs), and a top bar looks as it does in the picture.

- **Start:** the `windows` glyph, then `workspaces` for Winland's workspaces on that widget's own screen: 1 to 5 always, and any other that exists. An empty one is dimmed, the shown one is the tiling glyph, and a click shows it. Without Winland running there are no workspaces. On a side bar the buttons stack.
- **Center:** the `clock`, the day and time, such as `Saturday 20:25`, in your Windows regional format. A `timezone` setting names another zone, such as `UTC`; without one the clock is local, and two clocks can show two zones. On a side bar the hours sit above the minutes, such as `20` over `25`, and the tooltip stays the full date.
- **End:** `tray` on the primary screen only. Hovering its chevron slides out the icons Windows keeps in its overflow: out to the side on a top or bottom bar, and downwards on a side bar, where the icons stack. Then `bluetooth`, `network`, `audio`, `screen` and `battery`; each opens its Windows Settings page when clicked. The battery glyph is the charge, left out on a PC without one. It does not show health or a power mode. Bluetooth is left out when there is no adapter.

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
| [AppShell.cs](AppShell.cs) | Hides Explorer's taskbar, sets the style, registers each widget, and shows one [Bar](../../docs/configuration.md#a-bar-on-every-screen) with the default lists. |
| [TokyoNight.cs](TokyoNight.cs) | The shell's [style](../../docs/configuration.md#style): Tokyo Night's colors, the font, the slot size and the padding. |
| [Widgets/](Widgets) | One component per widget. A widget follows the service it shows with `UseObservable` or `UseObservableTree`. |
| [Glyphs.cs](Glyphs.cs) | Names each glyph with its Nerd Fonts name and chooses status glyphs. |
| [OmarchyStyle.csproj](OmarchyStyle.csproj) | References Winshell and copies the font files beside the program. |
| [Fonts/README.md](Fonts/README.md) | Records the bundled font's source and licences. |

## Change it

- **Colors, font and size:** edit [TokyoNight.cs](TokyoNight.cs). The widgets look nothing up: Winshell builds the `Button`, `TextBlock`, `FontIcon` and `ToolTip` styles from it. `SlotSize` is how thick the bar is, and how wide a button is, and `Padding` the space at its ends. The network widget paints its glyph in `Alert` when the connection can't reach the internet.
- **Move the bar or change its widgets:** edit the preferences file, `%APPDATA%\winshell\preferences\<shell id>.toml`, or `<WINSHELL_CONFIG_DIR>\preferences\<shell id>.toml` when that variable is set. Its `[bar]` section holds `edge` and the `start`, `center` and `end` lists, which replace the ones in [AppShell.cs](AppShell.cs) while the file has them. Saving applies it live. This moves the bar to the bottom, puts a UTC clock beside the local one and reverses the end:

  ```toml
  [bar]
  edge = "bottom"
  center = [{ widget = "clock" }, { widget = "clock", timezone = "UTC" }]
  end = [
    { widget = "battery" },
    { widget = "screen" },
    { widget = "audio" },
    { widget = "network" },
    { widget = "bluetooth" },
    { widget = "tray" },
  ]
  ```

  `edge` is `top`, `bottom`, `left` or `right`. Each list holds tables: `widget` names the widget, and the other keys are its settings.

- **Give one monitor its own bar:** add a `[[bar.screen]]` row to the same file. It may change `edge` and any of the three lists for that monitor alone. `winshell log` prints a `bar` line per screen that starts `manufacturer / model / serial`, which is the identity to copy. Match those three, never `\\.\DISPLAYn`, which Windows renumbers. A row for a monitor that is not connected stays in the file and is not shown on another screen.

  ```toml
  [[bar.screen]]
  manufacturer = "GSM"
  model = "LG ULTRAGEAR"
  serial = "ABC123"
  edge = "left"
  end = [{ widget = "tray" }, { widget = "battery" }]
  ```

  A row matches when manufacturer, model and serial all equal the monitor's, including an empty serial, and the first match wins. A key the row leaves out is the `[bar]` section's.

- **Live-edit a widget:** run `winshell dev --path examples/omarchy-style` and save a file in [Widgets/](Widgets). See [Develop a shell](../../docs/configuration.md#develop-a-shell) for what reaches the running shell and what restarts it.
- **Add a widget:** copy [Widgets/AudioWidget.cs](Widgets/AudioWidget.cs), follow another service with `UseObservable` or `UseObservableTree`, open its Settings page with `Props.Shell.Open`, and register the component in [AppShell.cs](AppShell.cs). Put that name in a list there or in the preferences file.
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
