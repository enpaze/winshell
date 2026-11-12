# Shells are built from public building blocks

Winshell gives a shell author three layers. Primitives are what exists today: windows, widget areas, services and screens. Building blocks sit on them: windows on each screen, component preferences, the shell style and `shell.Open`. Compositions put building blocks together for a common shape of desktop, and `Bar` is the first. A composition uses only public API, the same API a configuration has, and an architecture test holds it to that. So a dock, a sidebar, an overlay or any other desktop can be built the way `Bar` is, and `Bar` stays short enough to read and copy into a variant of your own.

## Considered options

- **A bar with access to Winshell's internals.** Shorter to write, but the bar would be the one easy desktop, and nobody could copy it.
- **Closing #1712 in favor of the bar.** A dock needs windows on each screen as much as a bar does, so they stay a building block.
- **A shell defined in a TOML or XAML file alone.** A second way to define a shell. Not now.

## Consequences

- `new Bar { Edge, Start, Center, End }` shows a bar on every screen, and `Dispose` removes it. The lists name widgets by the names they are registered under, and a plain string is a placement with no settings. A shell can have any number of bars. Each has a `Name`, `"bar"` unless given.
- A bar reserves its edge; `Reserve = false` makes it an overlay. It leaves Explorer's taskbar alone: hiding that stays one line of the configuration's.
- A bar is as thick as the style's slot size, across a top or bottom edge and along a left or right one. `Thickness` overrides it.
- Windows on each screen replace only the window of the screen that changed: one that comes or goes, or whose row in the component's preferences changes. They match a screen to its row by manufacturer, model and serial, never by the name Windows numbers. A bar's row can change its `edge` and any of its three lists, so a side bar needs no layout of its own. A change to the section replaces every screen's bar. A change to the shell style does not: a bar with no style of its own keeps its windows and takes the new style.
- Proposed name, settled in review: `ScreenWindows`, the windows a component keeps, one on each screen.
