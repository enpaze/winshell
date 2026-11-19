# PanelWindow

A `PanelWindow` is a frameless window that sits on one edge of a screen, such as a bar or a dock. It can reserve its thickness along that edge, so maximized windows stop short of it. In the `features` shell it is a small panel, 240 by 32 logical pixels, at the start of the top edge of the primary screen, which reserves nothing and sits behind other windows. It shows its real size, such as `panel window: 240x32`.

## Sub-features

- `edge` places the panel on the screen edge `Edge` names, `Edge.Top` unless set.
- `align` centers the panel along its edge, puts it at the edge's start or end, or stretches it across the edge with `Align`.
- `reserve` reserves the panel's thickness along its edge with `Reserve = true`, so maximized windows stop short of it: its `Height` on the top or bottom edge, its `Width` on the left or right.
- `above-windows` keeps the panel above other windows, or behind them with `AboveWindows = false`.
- `screen` places the panel on the `Screen` it is given, or on the primary screen.
- `covers-screen` fills the whole screen, not only the work area, with `CoversScreen`.
- `margins` keeps `Margins` between the panel and the screen's sides.
- `focusable` lets the panel take the keyboard with `Focusable`.
- `per-screen` keeps one panel on each screen with `ScreenWindows`.
- `component` shows the component `T` of a `PanelWindow<T>`, or of a `PanelWindow<T, TProps>` with the props it was given, on a surface, and unmounts it when the panel is disposed.

## How to get to it (user POV)

- In the function a shell passes to `Shell.Run`, write `shell.Add(new PanelWindow<T> { Edge = Edge.Bottom, Align = PanelAlign.Stretch, Height = 48, Reserve = true })` for a component `T`, or `shell.Add(new PanelWindow { ..., Content = ... })` for any WinUI element. A panel shows when it is added.
- Make one per screen with `shell.Add(new ScreenWindows("name", (screen, row) => new PanelWindow { Screen = screen, ... }))`.
- Every `Bar` is a `PanelWindow` on each screen.
- Start the `features` shell. Its panel is at the top left of the primary screen, behind other windows.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Read the panel's size.** Start the feature shell and find its panel. Run `winshell inspect --pid {pid} tree`. The first window whose kind is `PanelWindow` holds exactly one element with the automation id `block.panel-window`, and its `text` gives a width and height above zero, matching `^panel window: [1-9][0-9]*x[1-9][0-9]*$`.

```json
[
  {"element": "block.panel-window", "window": "PanelWindow", "has": {"text": "^panel window: [1-9][0-9]*x[1-9][0-9]*$"}}
]
```

## Gotchas

- The panel sits behind other windows, so the user's windows can hide it on screen. `tree` still reads it.
- The step proves the panel opened with a size. It does not check its place, its edge or that it reserves nothing; a screenshot or the window's `x` and `y` in the tree shows the place.
- The feature panel reserves nothing, so the work area does not change. A panel with `Reserve = true`, such as a bar, does change it while the shell runs.
- `AboveWindows` keeps a panel above other windows, except while an app fills that screen.
