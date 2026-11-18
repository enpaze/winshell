# winshell inspect key

`winshell inspect key` presses keys in a window of a running shell: one chord, such as `Enter`, or several separated by commas, such as `"Tab, Tab, Space"`, named as a hotkey names them. The keys go to the element that keyboard focus is on, and the command prints `Pressed <keys>`. It is real input. The driver proves only the refusal for a window that is not open, which the shell gives before it sends any key.

## Sub-features

- `key-plain` posts a key alone, such as `Tab` or `Enter`, to the window's own input site, whether the window is in front or not.
- `key-chord` sends a chord with Ctrl, Alt or Shift through the real keyboard, and refuses when the window, or a menu it opened, is not in front.
- `key-sequence` presses several chords in order, separated by commas.
- `key-taken` refuses a chord that never reaches the window in front: the Windows key, Ctrl+Esc, Ctrl+Shift+Esc, Alt+Tab, Alt+Esc, and a chord a program has registered as a hotkey.
- `key-window` sends the keys to the window that `[window]` names, by its content type or its number in `tree`, or to the first window without one.
- `key-in-use` refuses and sends nothing while someone has used the mouse or keyboard in the last 3 seconds.
- `key-missing` refuses a window that is not open, before it sends any key.

## How to get to it (user POV)

- Run `winshell inspect key <keys> [window]` in a terminal, such as `winshell inspect key "Ctrl+K, C"`.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-c <name>` or `-p <path>`. With none, the command acts on the only running shell.
- Run `winshell inspect focus <name>` first to put keyboard focus on the element that should get the keys.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- No open window of the feature shell shows content of the type `features.missing`.

- **Refuse a missing window.** Press Enter in a window the shell does not have open. Run `winshell inspect --pid {pid} key Enter features.missing`. It exits with 1 and says `No open window shows features.missing`, and no key is pressed.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "key", "Enter", "features.missing"], "exit": 1, "output": "No open window shows features\\.missing"}
]
```

## Gotchas

- This feature does not prove that a key arrives. A real `key` presses keys on the desktop, and the driver sends no real input. Do not report `key-plain`, `key-chord` or `key-sequence` as verified.
- The word after the keys is a window, not an element: the type of a window's content, such as `BarView`, or its number in `tree`. That is why the refusal says `No open window shows`, not `No element is named`.
- Keys that `winshell` can't read, and any chord with the Windows key, exit with 2 before the command reaches the shell.
- A key alone goes to the window's own input site, so a menu or flyout open in a window of its own does not get it.
- A program that watches keys with a keyboard hook, as AutoHotkey does, still sees a chord. Run real input only in a live session that nobody else is using.
