# starter battery widget

The starter's `battery` widget is the text at the end of its bar that shows the battery's charge as a percentage, such as `76%` or `76 %`, in the Windows regional format. Its automation name is `Battery`. On a PC with no battery the text is collapsed.

## Sub-features

- `charge` shows the charge of the battery Windows reports as a whole percentage.
- `follow` changes as the charge changes.
- `no-battery` collapses the text on a PC with no battery.

## How to get to it (user POV)

- Run `winshell new starter` and then `winshell run -c starter`.
- The starter finds `Battery` as the widget `battery` and places it in the bar's `End` list.
- Place it elsewhere with `"battery"` in a list of the `[bar]` section of the preferences file, or with `Start = ["battery"]` in the starter's code.

## Driving it with winshell inspect

Preconditions:

- Shell: `starter`.

- **Read the charge.** Start the starter and look at the right end of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation name `Battery`, and its `text` is a whole percentage, matching `^[0-9]+\s?%$`.

```json
[
  {"element": "Battery", "window": "Surface", "has": {"text": "^[0-9]+\\s?%$"}}
]
```

## Gotchas

- On a PC with no battery, as on CI, the text is collapsed but still in the tree with a percentage, so the step passes. The element has `"visible": false` in `tree-<n>.json` then; check it before you report a real charge.
- The percent format follows the Windows regional format. A format that puts `%` first, such as `%76`, does not match the pattern.
- The starter's bar reserves the top edge of every screen while it runs.
