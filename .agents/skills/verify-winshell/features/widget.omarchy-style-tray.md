# omarchy-style tray widget

The `tray` widget is the row of notification area icons on the omarchy-style bar, on the primary screen only. It shows the icons apps add, and a chevron, `Show hidden icons`, before them when Windows keeps some in its overflow. Hovering the chevron slides those icons out. Choosing an icon clicks it as Explorer's tray does, and right-clicking it asks the app for its menu. Hovering an icon shows its tooltip, or the app's name.

## Sub-features

- `icons` shows each tray icon that is not in the overflow, named by its app.
- `overflow` shows a `Show hidden icons` chevron when Windows keeps icons in its overflow, and slides them out while the pointer is over it.
- `click` passes a click on an icon to its app.
- `menu` asks the app for its menu on a right click.
- `primary-only` shows icons on the primary screen's bar only, and an empty row on the others.
- `vertical` stacks the icons on a side bar, and the overflow slides down.

## How to get to it (user POV)

- Run `winshell new omarchy-style` and then `winshell run -s omarchy-style`, or `winshell run --path examples/omarchy-style` from a clone.
- The example finds `TrayWidget` as the widget `tray` and places it first in the bar's `End` list.
- Place it elsewhere with `"tray"` in a list of the `[bar]` section of the preferences file.
- Choose or right-click an icon, or point at the chevron, near the right end of the bar.

## Driving it with winshell inspect

Preconditions:

- Shell: `omarchy-style`.

- **Find the tray row.** Start the omarchy-style shell and look at the end of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation id `tray`.

```json
[
  {"element": "tray", "window": "Surface"}
]
```

## Gotchas

- The icons depend on the apps that run, so the step proves only that the row exists. With no icons, or on a screen that is not the primary one, the row is collapsed and the step still passes.
- Before the widget's window is known, the element is a hidden text block with the same id, which also passes. Check the `type` in `tree-<n>.json`.
- Only one instance in the Windows session hosts the tray icons. Another shell that hosts them, such as a second Winshell, leaves this one waiting.
- Some apps add their icon only when they start, so their icon shows only after they start again while the shell runs.
- Do not choose an icon with `inspect click`: it clicks that app's real tray icon.
