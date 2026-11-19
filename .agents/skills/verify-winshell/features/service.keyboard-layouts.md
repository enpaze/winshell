# Keyboard layouts

`shell.KeyboardLayouts` reads the input languages that Windows has installed and the one that the active window uses, and switches between them. A shell uses it for a language indicator, such as `ENG`. The feature shell's row `service.keyboard-layouts` shows `current: <label>; layouts: N`, where the label is the current layout's short label, or `none` when Windows reports no current layout.

## Sub-features

- `layouts` `Layouts` lists each installed `KeyboardLayout`, with its `Id`, `Name` and `Label`, a short uppercase code of up to three letters.
- `current` `Current` is the layout of the window in the foreground.
- `use` `Use(layout)` switches the foreground window to that layout, and returns `false` when the layout is not installed or is already current.
- `next` `Next()` switches the foreground window to the next installed layout.
- `sampling` the service reads the layouts every half second while something follows it, and stops when nothing does.

## How to get to it (user POV)

- A shell reads `shell.KeyboardLayouts`, or `Shell.KeyboardLayouts` in a widget, and follows it with `UseObservable` or `PropertyChanged`.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the layouts.** Read the `service.keyboard-layouts` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.keyboard-layouts`, and its `text` matches `^current: .+; layouts: [1-9][0-9]*$`, so Windows has at least one layout installed. The driver retries for 20 seconds.

```json
[
  {"element": "service.keyboard-layouts", "has": {"text": "^current: .+; layouts: [1-9][0-9]*$"}, "within": 20}
]
```

## Gotchas

- `Current` follows the foreground window, which is usually another app's window, not the shell's. The step accepts any label, and `none`.
- `Layouts` and `Current` are empty until something follows the service. The row follows it, so the list fills within a moment.
- `Use` and `Next` change the input language of the user's foreground window. No step calls them.
