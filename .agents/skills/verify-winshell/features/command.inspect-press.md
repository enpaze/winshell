# winshell inspect press

`winshell inspect press` presses and releases the real left mouse button on the middle of an element of a running shell, or the right one with `--right`, so the pressed look, `Click`, `RightTapped` and the rest run. It prints `Pressed <name>` or `Right-pressed <name>`, and the cursor goes back as it does for `leave`. The driver proves only the refusal for a name that matches no element, which the shell gives before it moves the cursor.

## Sub-features

- `press-left` presses and releases the left mouse button on the element's middle.
- `press-right` presses the right mouse button instead with `--right`.
- `press-back` puts the cursor back afterwards, and says so when it couldn't because the mouse moved.
- `press-window` searches only the window that `[window]` names, by its content type or its number in `tree`.
- `press-covered` refuses when another window is on top at the element's middle, or the element is clipped, covered, not hit-testable or has no size.
- `press-in-use` refuses and sends nothing while someone has used the mouse or keyboard in the last 3 seconds, or when the mouse moves while it works.
- `press-missing` refuses a name that matches no element, before it moves the cursor.

## How to get to it (user POV)

- Run `winshell inspect press <name> [window] [--right]` in a terminal, where `<name>` is an `x:Name`, an automation id or an automation name.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>`. With none, the command acts on the only running shell.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- No element in the feature shell is named `features.missing`.

- **Refuse a missing element.** Press the mouse on an element the shell does not have. Run `winshell inspect --pid {pid} press features.missing`. It exits with 1 and says `No element is named features.missing`, and the cursor does not move.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "press", "features.missing"], "exit": 1, "output": "No element is named features\\.missing"}
]
```

## Gotchas

- This feature does not prove that a press arrives. A real `press` moves the user's cursor and clicks, and the driver sends no real input. Do not report `press-left` or `press-right` as verified. Use `winshell inspect click` to activate a control without real input.
- A press can activate a window that may then take the keyboard.
- `--right` works with `press` only. On any other `inspect` command it exits with 2.
- The shell looks up the name before it checks for use of the PC, so this refusal comes even while someone moves the mouse.
- Run real input only in a live session that nobody else is using, never at someone's active desktop.
