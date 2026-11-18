# starter clock widget

The starter's `clock` widget is the text in the center of its bar that shows the time as Windows is set to write it, such as `14:05` or `2:05 PM`. It updates each second. Its automation name is `Clock`.

## Sub-features

- `time` shows the short time in the Windows regional format.
- `tick` renders again each second, so the minute changes on time.

## How to get to it (user POV)

- Run `winshell new starter` and then `winshell run -c starter`.
- The starter registers it with `Widgets.Register<Clock>("clock")` and places it in the bar's `Center` list.
- Run the starter under `winshell dev -c starter` and change `{time:t}` to `{time:T}` in `Clock.cs` to show the seconds.

## Driving it with winshell inspect

Preconditions:

- Shell: `starter`.

- **Read the time.** Start the starter and look at the center of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation name `Clock`, and its `text` starts with a time, matching `^[0-9]{1,2}[:.][0-9]{2}`.

```json
[
  {"element": "Clock", "window": "Surface", "has": {"text": "^[0-9]{1,2}[:.][0-9]{2}"}}
]
```

## Gotchas

- The time follows the Windows regional format. A format that starts with an AM or PM designator, such as `오후 2:05`, does not match the pattern.
- The bar shows on the next turn of the shell's loop, so a tree read right after start can miss it. The element step retries for 10 seconds.
- `winshell dev` builds the starter first and runs until Ctrl+C. End a shell that `winshell run` started with `winshell kill` before you start `dev`, as the configuration guide does.
- The starter's bar reserves the top edge of every screen while it runs.
