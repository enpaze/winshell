# Hotkeys (NewHotkey)

`shell.NewHotkey()` makes a `Hotkey`, a key chord that works anywhere in Windows while the shell runs, and raises `Pressed` and `Released` when the user presses it. A configuration uses it to open a launcher or switch a workspace from the keyboard. The feature shell registers `Ctrl+Alt+Shift+F13`, and its row `service.new-hotkey` shows `sequence: Ctrl+Alt+Shift+F13; active: <True|False>; taken: <True|False>`.

## Sub-features

- `sequence` `Sequence` takes a chord of `Win`, `Ctrl`, `Alt` and `Shift` and a key, such as `Win+E`, and a comma separates the chords of a sequence.
- `active` `Active` is `True` while the hotkey is registered.
- `taken` `Taken` is `True` when Windows or another app, another Winshell instance included, already holds the chord.
- `error` `Error` says why a sequence is not a hotkey, such as `Super`, `Meta`, or a left or right modifier.
- `enabled` setting `Enabled = false` releases the hotkey, and `true` registers it again.
- `press` `Pressed`, `Released` and `IsPressed` follow the user's key press.
- `win-tap` a tap of the Windows key reaches the shell only while a `Hotkey` with `Sequence` of `Win` is enabled.

## How to get to it (user POV)

- A configuration calls `shell.NewHotkey()`, sets `Sequence`, and handles `Pressed`.
- The user presses the chord anywhere in Windows.
- The starter and the omarchy-style example do not register one.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the hotkey's state.** Read the `service.new-hotkey` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.new-hotkey`, and its `text` matches `^sequence: Ctrl\+Alt\+Shift\+F13; active: (True|False); taken: (True|False)$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.new-hotkey", "has": {"text": "^sequence: Ctrl\\+Alt\\+Shift\\+F13; active: (True|False); taken: (True|False)$"}, "within": 20}
]
```

## Gotchas

- No step presses the chord, because that would send real keys. The step proves the registration state, not `Pressed`.
- Another app or another Winshell instance that holds `Ctrl+Alt+Shift+F13` makes the row show `active: False; taken: True`. The step accepts that, so check `Taken` before you conclude the registration failed.
- While the feature shell runs, the chord is registered for the whole Windows session.
