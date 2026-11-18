# Bar

A `Bar` is a panel along one edge of every screen, with three widget areas: start, center and end. The user sees a strip on each screen that reserves its edge, so maximized windows stop short of it. In the `starter` shell it runs along the top, with the clock in the center and the battery at the end, and `winshell inspect tree` lists one `PanelWindow` per screen whose content is a `Surface`.

## Sub-features

- `areas` places the widgets named in `Start`, `Center` and `End`, in order, each by its widget name.
- `every-screen` shows one bar on each screen, and follows screens as they are connected and removed.
- `edge` runs along the top, bottom, left or right edge; on a side edge the areas run from top to bottom.
- `reserve` reserves the edge, or lays the bar over maximized windows with `Reserve = false`.
- `thickness` is the style's slot size, or `Thickness`; the ends keep the style's padding, or `Padding`.
- `style` takes the bar's own `Style` over the shell's.
- `preferences` reads `edge` and the three lists from the bar's section of the preferences file, where each item is a widget name or a table, and applies a save live.
- `per-screen` lets a `[[bar.screen]]` row change the edge or the lists for one monitor.
- `several` lets a shell have several bars, each with its own `Name` and preferences section.

## How to get to it (user POV)

- Run `winshell new starter` and then `winshell run -c starter`. The starter makes one bar.
- In the function a shell passes to `Shell.Run`, write `shell.Add(new Bar { Edge = Edges.Top, Center = ["clock"], End = ["battery"] });`. Each name is a widget of the shell project, such as the class `Clock` or `ClockWidget` for `clock`.
- Edit the `[bar]` section of `preferences\<shell id>.toml` in the config folder, or a `[[bar.screen]]` row, and save it.

## Driving it with winshell inspect

Preconditions:

- Shell: `starter`.
- The session's preferences file has no `[bar]` lists of its own, so the bar shows the starter's defaults.

- **Read the bar.** Start the starter and look at the bar on the first screen. Run `winshell inspect --pid {pid} tree`. The first window whose kind is `PanelWindow` holds exactly one element named `Clock`, and its `text` is a time that matches `^[0-9]{1,2}[:.][0-9]{2}`.

```json
[
  {"element": "Clock", "window": "PanelWindow", "has": {"text": "^[0-9]{1,2}[:.][0-9]{2}"}}
]
```

## Gotchas

- The bar reserves the top edge of every screen while the shell runs, so the user's maximized windows shrink. `winshell kill` gives the edge back.
- The step reads only the first bar window in the tree. A second screen's bar, the reserved edge and the clock's place in the center are not checked; take a screenshot with `winshell inspect --pid {pid} screenshot <file> Surface` to see them. In `inspect` commands, a `[window]` is the window's content type or number, not its kind.
- A bar shows on the next turn of the shell's loop, not at once, so a tree read right after start can miss it. The element step retries for 10 seconds.
- `{time:t}` follows the Windows regional format. A format that starts with an AM or PM designator, such as `오후 2:05`, does not match the pattern.
- A `[bar]` list in the preferences file replaces the one in code. A value that can't be used is logged under `preferences`, and the bar keeps the one it had.
