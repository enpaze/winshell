# FloatingWindow

A `FloatingWindow` is a normal window with a title bar, which the user can move, resize, minimize and close. In the `features` shell it is the main window, titled `Winshell features` and 460 by 760 logical pixels, which holds every service row. Its first row shows the window's real size, such as `floating window: 460x760`.

## Sub-features

- `title` shows `Title` in the window's title bar.
- `size` opens at `Width` and `Height`, kept between `MinimumSize` and `MaximumSize`.
- `state` minimizes, maximizes or fills the screen with `Minimized`, `Maximized` and `Fullscreen`.
- `owner` makes the window owned by another `FloatingWindow` with `Owner`.
- `content` shows any WinUI element as its `Content`, such as a `ReactorHostControl`.
- `look` takes a `Backdrop` of Mica, Acrylic or MicaAlt, and rounded or square `Corners`.

## How to get to it (user POV)

- In a configuration's `Start`, write `new FloatingWindow { Title = "...", Width = 460, Height = 760, Content = ..., Visible = true }`.
- Start the `features` shell. Its window is the one titled `Winshell features`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The `Winshell features` window is open.

- **Read the window's size.** Start the feature shell and look at its main window. Run `winshell inspect --pid {pid} tree`. The first window whose kind is `FloatingWindow` holds exactly one element with the automation id `block.floating-window`, and its `text` gives a width and height above zero, matching `^floating window: [1-9][0-9]*x[1-9][0-9]*$`.

```json
[
  {"element": "block.floating-window", "window": "FloatingWindow", "has": {"text": "^floating window: [1-9][0-9]*x[1-9][0-9]*$"}}
]
```

## Gotchas

- The step proves the window opened with a size. It does not check the title or the exact 460 by 760, which a tiling window manager or the user can change.
- With Winland running and no window rule that ignores `winshell`, Winland tiles the window and changes its size. Never add the rule or reload Winland yourself.
- Closing the window hides it, and `tree` lists only open windows, so the step finds nothing. Start the shell again.
- The window opens on the user's desktop, among their windows. Leave the desktop as you found it.
