# Omarchy-style panel

A Winshell configuration that draws the top bar of [Omarchy](https://github.com/omacom/omarchy) v4.5.0 as a panel, in Omarchy's Tokyo Night style. Copy from it: it shows a panel per screen, Winland's workspaces, the tray, and the status of Bluetooth, the network, audio, the screen and the battery, each read from a Winshell service.

![The panel: the Windows glyph and workspaces 1 to 5 on the left, the clock in the middle, the tray chevron, tray icons and status glyphs on the right](screenshot.png)

## What it shows

One panel along the top of every screen, sized by `PanelHeight` in [PanelStyles.xaml](PanelStyles.xaml). It reserves that edge, and Explorer's taskbar is hidden while the shell runs; Winshell shows the taskbar again when the shell quits. The colors are Tokyo Night's, defined in [TokyoNight.xaml](TokyoNight.xaml), with an alert color for a network that can't reach the internet, whatever the Windows theme is.

- **Left:** the Windows glyph, then Winland's workspaces on that screen: 1 to 5 always, and any other that exists. An empty one is dimmed, the shown one is the tiling glyph, and a click shows it. Without Winland running there are no workspaces.
- **Centre:** the day and time, such as `Saturday 20:25`, in your Windows regional format.
- **Right:** on the primary screen, the tray. Hovering its chevron slides out the icons Windows keeps in its overflow. Then Bluetooth, the network, audio, the screen and the battery; each opens its Windows Settings page when clicked. The battery is left out on a PC without one.

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
| [PanelView.xaml](PanelView.xaml) | Lays out the left, centre and right sections and names the Settings pages. |
| [PanelStyles.xaml](PanelStyles.xaml) | Merges Tokyo Night, names the sizes, and defines the button template and tooltip styles. |
| [PanelView.xaml.cs](PanelView.xaml.cs) | Sets up the clock, refreshes the glyphs and watchers when the layout loads, disposes them, and shares the glyph and tooltip helpers. |
| [PanelView.Workspaces.cs](PanelView.Workspaces.cs) | Chooses and draws this screen's workspace buttons and handles their clicks. |
| [PanelView.Tray.cs](PanelView.Tray.cs) | Draws the tray icons and slides out the overflow on hover. |
| [PanelView.Status.cs](PanelView.Status.cs) | Draws Bluetooth, network, audio, screen and battery status and opens Settings. |
| [TokyoNight.xaml](TokyoNight.xaml) | Defines the style's brushes and bundled font. |
| [Glyphs.cs](Glyphs.cs) | Names each glyph with its Nerd Fonts name and chooses status glyphs. |
| [Watcher.cs](Watcher.cs) | Renders a section again when a service object or list it read changes; [#1706](https://github.com/enpaze/winshell/issues/1706) will replace it with automatic tracking. |
| [TrayIcons.cs](TrayIcons.cs) | Converts Winshell's tray pixels into a WinUI bitmap. |
| [OmarchyStyle.csproj](OmarchyStyle.csproj) | References Winshell and copies the font files beside the program. |
| [Fonts/README.md](Fonts/README.md) | Records the bundled font's source and licences. |

## Change it

- **Colors and font:** replace [TokyoNight.xaml](TokyoNight.xaml) with another style dictionary using the same resource names.
- **Size and controls:** edit [PanelStyles.xaml](PanelStyles.xaml) for the button template, tooltip styles and named sizes: `PanelHeight` for the panel and its reserved edge, `SlotWidth` for status and tray buttons, `IconSize` for tray icons, `PanelFontSize` for text and glyphs, `WorkspaceWidth` for workspace buttons, and `PanelInset` for the edges. The C# reads these resources too.
- **Live-edit the layout:** run `winshell dev --path examples/omarchy-style` and save [PanelView.xaml](PanelView.xaml). Its markup avoids `x:Bind` so live editing keeps working without a rebuild or restart; C# sets the Windows and tray chevron glyphs when the layout loads. Edits to the resource dictionaries rebuild and restart the shell; see [Develop a shell](../../docs/configuration.md#develop-a-shell).
- **Add a section:** copy the audio button in [PanelView.xaml](PanelView.xaml) and `ShowAudio` in [PanelView.Status.cs](PanelView.Status.cs), then add its watcher in [PanelView.xaml.cs](PanelView.xaml.cs). Give the button its own name and Settings URI in `Tag`.
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
