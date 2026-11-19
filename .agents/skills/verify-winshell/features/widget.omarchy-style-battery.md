# omarchy-style battery widget

The `battery` widget is the last button on the omarchy-style bar. Its glyph shows the battery's charge and whether the PC is on battery. Hovering it shows the charge and its state, such as `76% · On battery`, `100% · Fully charged`, `80% · Not charging` or `54% · Charging`, and choosing it opens the Power & battery page of Windows Settings. On a PC with no battery the button is collapsed.

## Sub-features

- `glyph` shows the charge, and whether the PC runs on battery.
- `label` gives the percentage and `On battery`, `Fully charged`, `Not charging` or `Charging`, as the tooltip and the automation name.
- `follow` changes as the charge, the power source or the battery's state changes.
- `open-settings` opens `ms-settings:powersleep` when chosen.
- `no-battery` collapses the button on a PC with no battery.

## How to get to it (user POV)

- Run `winshell new omarchy-style` and then `winshell run -c omarchy-style`, or `winshell run --path examples/omarchy-style` from a clone.
- The example finds `BatteryWidget` as the widget `battery` and places it last in the bar's `End` list.
- Place it elsewhere with `"battery"` in a list of the `[bar]` section of the preferences file.
- Choose the battery glyph at the right end of the bar.

## Driving it with winshell inspect

Preconditions:

- Shell: `omarchy-style`.

- **Read the battery button.** Start the omarchy-style shell and look at the right end of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation id `battery`, its `type` is `Button`, and its `label` is a percentage and a state, matching `^[0-9]+% · (On battery|Fully charged|Not charging|Charging)$`.

```json
[
  {"element": "battery", "window": "Surface", "has": {"type": "^Button$", "label": "^[0-9]+% · (On battery|Fully charged|Not charging|Charging)$"}}
]
```

## Gotchas

- On a PC with no battery, as on CI, the button is collapsed but still in the tree with a label, so the step passes. The element has `"visible": false` in `tree-<n>.json` then; check it before you report a real charge.
- Any state that is not on battery, fully charged or not charging reads `Charging`.
- Do not run `inspect click battery`: it opens Windows Settings on the user's desktop.
- The omarchy-style shell hides Explorer's taskbar while it runs and puts it back when it exits.
