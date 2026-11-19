# omarchy-style screen widget

The `screen` widget is a button on the omarchy-style bar that names the screen its bar is on. Its glyph is one screen, or two screens when more than one is connected. Hovering it shows the monitor's model, or the screen's name when Windows reports no model, and choosing it opens the Display page of Windows Settings.

## Sub-features

- `label` names the monitor model of the bar's screen, or the screen's name, as the tooltip and the automation name.
- `glyph` shows one screen, or several when more than one is connected.
- `follow` changes as screens are connected and removed and as the bar moves to another screen.
- `open-settings` opens `ms-settings:display` when chosen.

## How to get to it (user POV)

- Run `winshell new omarchy-style` and then `winshell run -s omarchy-style`, or `winshell run --path examples/omarchy-style` from a clone.
- The example finds `ScreenWidget` as the widget `screen` and places it in the bar's `End` list.
- Place it elsewhere with `"screen"` in a list of the `[bar]` section of the preferences file.
- Choose the screen glyph near the right end of the bar.

## Driving it with winshell inspect

Preconditions:

- Shell: `omarchy-style`.

- **Read the screen button.** Start the omarchy-style shell and look at the end of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation id `screen`, its `type` is `Button`, and its `label` is not empty, matching `^.+$`.

```json
[
  {"element": "screen", "window": "Surface", "has": {"type": "^Button$", "label": "^.+$"}}
]
```

## Gotchas

- Each screen has its own bar, and the step reads only the first bar's button. Its label names that bar's screen.
- Until the bar's window has a screen, the element is hidden and its label is empty. The step retries for 10 seconds.
- Do not run `inspect click screen`: it opens Windows Settings on the user's desktop.
