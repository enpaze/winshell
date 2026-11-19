# omarchy-style clock widget

The `clock` widget is the text in the center of the omarchy-style bar. It shows the day and the time in 24 hours, such as `Saturday 20:25`, in the Windows regional format, and hovering it shows the full date. On a left or right bar the hours sit above the minutes. A `timezone` setting shows the time in another zone.

## Sub-features

- `face` shows the day name and `HH:mm`, and changes when the minute changes.
- `vertical` puts the hours above the minutes on a side bar.
- `timezone` shows the time of the Windows time zone id in its `timezone` setting, such as `UTC`, and adds the zone to the tooltip.
- `tooltip` shows the full date when hovered.
- `bad-setting` refuses a `timezone` that is not a string, and the area shows a placeholder with the reason, `timezone must be a string.`, in its place.

## How to get to it (user POV)

- Run `winshell new omarchy-style` and then `winshell run -s omarchy-style`, or `winshell run --path examples/omarchy-style` from a clone.
- The example finds `ClockWidget` as the widget `clock` and places it in the bar's `Center` list.
- Add another clock with `{ widget = "clock", timezone = "UTC" }` in a list of the `[bar]` section of the preferences file.

## Driving it with winshell inspect

Preconditions:

- Shell: `omarchy-style`.

- **Read the clock.** Start the omarchy-style shell and look at the center of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation id `clock`, and its `text` is a day name and a time, matching `^\w+ [0-9]{2}:[0-9]{2}$`.

```json
[
  {"element": "clock", "window": "Surface", "has": {"text": "^\\w+ [0-9]{2}:[0-9]{2}$"}}
]
```

## Gotchas

- The day name and the time separator follow the Windows regional format. A day name with a hyphen, such as `segunda-feira`, or a separator other than `:` does not match the pattern.
- A second clock placed in the same bar makes two elements with the automation id `clock`, and the step finds more than one.
- On a side bar the text has a line break between the hours and the minutes, and does not match the pattern.
- The omarchy-style shell hides Explorer's taskbar while it runs and puts it back when it exits.
