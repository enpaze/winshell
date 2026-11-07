# Omarchy-style panel

A Winshell configuration that draws the top bar of [Omarchy](https://github.com/omacom/omarchy) v4.5.0 as a panel, in Omarchy's Tokyo Night style. Copy from it: it shows a panel per screen, Winland's workspaces, the tray, and the status of Bluetooth, the network, audio, the screen and the battery, each read from a Winshell service.

![The panel: the Windows glyph and workspaces 1 to 5 on the left, the clock in the middle, the tray chevron, tray icons and status glyphs on the right](screenshot.png)

## What it shows

One panel, 26 pixels high, along the top of every screen. It reserves that edge, and Explorer's taskbar is hidden while the shell runs; Winshell shows the taskbar again when the shell quits. The colors are Tokyo Night's, `#1a1b26` behind `#a9b1d6`, with `#f7768e` for a network that can't reach the internet, whatever the Windows theme is.

- **Left:** the Windows glyph, then Winland's workspaces on that screen: 1 to 5 always, and any other that exists. An empty one is dimmed, the shown one is the tiling glyph, and a click shows it. Without Winland running there are no workspaces.
- **Centre:** the day and time, such as `Saturday 20:25`, in your Windows regional format.
- **Right:** on the primary screen, the tray. Hovering its chevron slides out the icons Windows keeps in its overflow. Then Bluetooth, the network, audio, the screen and the battery; each opens its Windows Settings page when clicked. The battery is left out on a PC without one.

Hover any glyph for what it stands for.

## Run it

From a clone of Winshell, with `winshell` [built from that clone](../../docs/development.md#build-from-source):

```powershell
winshell run --path examples/omarchy-style
```

The project references Winshell's source, so the repo's own check builds it.

## Use it as your own configuration

Copy the folder out of the repo, then reference the `Winshell` package instead of the source. In `OmarchyStyle.csproj`, delete the `<Import>` of `Winshell.props` and replace the `<ProjectReference>` with:

```xml
<PackageReference Include="Winshell" Version="<version>" />
```

The package is not published yet, so pack it from the same clone and point NuGet at it, as [Create one](../../docs/configuration.md#create-one) in the configuration guide shows. `<version>` is the release in the package's name.

## Credits

- The look, the sizes, the glyph choices and the colors come from [Omarchy v4.5.0](https://github.com/omacom/omarchy/tree/v4.5.0) by David Heinemeier Hansson, under the MIT licence. No Omarchy code, logo or font is copied: the Windows glyph stands where Omarchy's logo is.
- [JetBrainsMono Nerd Font](https://github.com/ryanoasis/nerd-fonts/releases/tag/v3.5.1), from Nerd Fonts v3.5.1, ships in `Fonts/`: the Regular weight only. JetBrains Mono is under the SIL Open Font License 1.1, in [Fonts/OFL.txt](Fonts/OFL.txt). Its icon sets keep their own licences, which [Fonts/README.md](Fonts/README.md) lists.
