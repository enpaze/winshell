# omarchy-style windows widget

The `windows` widget is the Windows glyph at the start of the omarchy-style bar, where Omarchy shows its logo. It is one slot of the style wide, and it does nothing when chosen.

## Sub-features

- `glyph` shows the Windows glyph from the bundled Nerd Font, centered in one slot.

## How to get to it (user POV)

- Run `winshell new omarchy-style` and then `winshell run -c omarchy-style`, or `winshell run --path examples/omarchy-style` from a clone.
- The example registers it with `Widgets.Register<WindowsWidget>("windows")` and places it first in the bar's `Start` list.
- Place it elsewhere with `"windows"` in a list of the `[bar]` section of the preferences file.

## Driving it with winshell inspect

Preconditions:

- Shell: `omarchy-style`.

- **Read the glyph.** Start the omarchy-style shell and look at the left end of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation id `windows`, and its `text` is one glyph, matching `^.{1,2}$`.

```json
[
  {"element": "windows", "window": "Surface", "has": {"text": "^.{1,2}$"}}
]
```

## Gotchas

- The glyph is outside the Basic Multilingual Plane, so its text is two UTF-16 characters. That is why the pattern accepts two.
- The text proves which character the widget shows, not that the bundled font drew it. Take a screenshot and look at it to check.
- The omarchy-style shell hides Explorer's taskbar while it runs and puts it back when it exits.
