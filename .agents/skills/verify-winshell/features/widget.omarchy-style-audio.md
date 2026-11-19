# omarchy-style audio widget

The `audio` widget is a button at the end of the omarchy-style bar that shows the default playback device. Its glyph is headphones for a headset or headphones, a muted speaker when the device is muted or at 0, and otherwise a speaker for a quiet, low or high volume. Hovering it shows `<device>: <volume>%` or `<device>: muted`, and choosing it opens the Sound page of Windows Settings. With no playback device the button is collapsed and its label is empty.

## Sub-features

- `glyph` chooses headphones, muted, or a speaker for the volume.
- `label` names the device and its volume, or `muted`, as the tooltip and the automation name.
- `follow` changes as the default playback device, its volume or its mute changes.
- `open-settings` opens `ms-settings:sound` when chosen.
- `no-device` collapses the button when there is no playback device.

## How to get to it (user POV)

- Run `winshell new omarchy-style` and then `winshell run -s omarchy-style`, or `winshell run --path examples/omarchy-style` from a clone.
- The example finds `AudioWidget` as the widget `audio` and places it in the bar's `End` list.
- Place it elsewhere with `"audio"` in a list of the `[bar]` section of the preferences file.
- Choose the speaker glyph near the right end of the bar.

## Driving it with winshell inspect

Preconditions:

- Shell: `omarchy-style`.

- **Read the audio button.** Start the omarchy-style shell and look at the end of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation id `audio`, its `type` is `Button`, and its `label` is `<device>: <volume>%`, `<device>: muted`, or empty, matching `^(.+: ([0-9]+%|muted))?$`.

```json
[
  {"element": "audio", "window": "Surface", "has": {"type": "^Button$", "label": "^(.+: ([0-9]+%|muted))?$"}}
]
```

## Gotchas

- A PC or a CI runner with no playback device has an empty label and a collapsed button, and the step still passes. Read the label in `tree-<n>.json` to see which state the run proved.
- Do not run `inspect click audio`: it opens Windows Settings on the user's desktop.
- The omarchy-style shell hides Explorer's taskbar while it runs and puts it back when it exits. `winshell kill` ends it.
- With headphones or a headset, the glyph is headphones even when muted. The label still says `muted`.
