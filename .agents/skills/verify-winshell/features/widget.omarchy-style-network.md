# omarchy-style network widget

The `network` widget is a button on the omarchy-style bar that shows the connection, preferring a wired one. Its glyph is Ethernet, Wi-Fi with its signal strength, or disconnected. Hovering it shows the network's name, `<name>: no internet`, `<name>: sign in to the network`, or `Disconnected`, and choosing it opens the network status page of Windows Settings. A connection that can't reach the internet paints the glyph in the style's alert color.

## Sub-features

- `glyph` shows Ethernet, Wi-Fi in five strengths, or disconnected, and a restricted glyph for a connection with no internet.
- `label` names the network, or says `Disconnected`, as the tooltip and the automation name.
- `restricted` adds `no internet` or `sign in to the network` to the label and paints the glyph in the alert color.
- `follow` changes as devices connect and disconnect and as the connectivity changes.
- `open-settings` opens `ms-settings:network-status` when chosen.

## How to get to it (user POV)

- Run `winshell new omarchy-style` and then `winshell run -c omarchy-style`, or `winshell run --path examples/omarchy-style` from a clone.
- The example finds `NetworkWidget` as the widget `network` and places it in the bar's `End` list.
- Place it elsewhere with `"network"` in a list of the `[bar]` section of the preferences file.
- Choose the network glyph near the right end of the bar.

## Driving it with winshell inspect

Preconditions:

- Shell: `omarchy-style`.

- **Read the network button.** Start the omarchy-style shell and look at the end of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation id `network`, its `type` is `Button`, and its `label` is not empty, matching `^.+$`.

```json
[
  {"element": "network", "window": "Surface", "has": {"type": "^Button$", "label": "^.+$"}}
]
```

## Gotchas

- The label is never empty, because a PC with no connection reads `Disconnected`. The step proves the button names a state, not which one; read the `label` in `tree-<n>.json`.
- The name is the network's profile name when Windows has one, else the adapter's name.
- Wi-Fi networks in range need Windows' Wi-Fi access and location services. The connected network shows without them.
- Do not run `inspect click network`: it opens Windows Settings on the user's desktop.
