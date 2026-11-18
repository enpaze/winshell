# omarchy-style bluetooth widget

The `bluetooth` widget is a button on the omarchy-style bar that shows the state of the default Bluetooth adapter. Its glyph is off, on, or connected. Hovering it shows `Bluetooth off`, `Bluetooth on`, or the names of the connected devices, one a line, and choosing it opens the Bluetooth page of Windows Settings. With no adapter the button is collapsed and its label is empty.

## Sub-features

- `state` shows the off, on or connected glyph for the default adapter.
- `label` says `Bluetooth off` or `Bluetooth on`, or lists the connected devices, as the tooltip and the automation name.
- `follow` changes as the adapter is turned on or off and as devices connect.
- `open-settings` opens `ms-settings:bluetooth` when chosen.
- `no-adapter` collapses the button when there is no adapter.

## How to get to it (user POV)

- Run `winshell new omarchy-style` and then `winshell run -c omarchy-style`, or `winshell run --path examples/omarchy-style` from a clone.
- The example registers it with `Widgets.Register<BluetoothWidget>("bluetooth")` and places it after the tray in the bar's `End` list.
- Place it elsewhere with `"bluetooth"` in a list of the `[bar]` section of the preferences file.
- Choose the Bluetooth glyph near the right end of the bar.

## Driving it with winshell inspect

Preconditions:

- Shell: `omarchy-style`.

- **Find the Bluetooth button.** Start the omarchy-style shell and look at the end of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation id `bluetooth`, and its `type` is `Button`.

```json
[
  {"element": "bluetooth", "window": "Surface", "has": {"type": "^Button$"}}
]
```

## Gotchas

- The step proves only that the button exists. Its label needs an adapter, which CI lacks; on a PC with one, read the `label` in `tree-<n>.json` for `Bluetooth on`, `Bluetooth off` or the device names.
- With no adapter the button is collapsed, `"visible": false` in the tree, and the step still passes.
- Do not run `inspect click bluetooth`: it opens Windows Settings on the user's desktop.
- The omarchy-style shell hides Explorer's taskbar while it runs and puts it back when it exits.
