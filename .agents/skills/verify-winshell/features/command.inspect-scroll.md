# winshell inspect scroll

`winshell inspect scroll` turns the real mouse wheel over an element of a running shell, one notch a step: `3` scrolls down and `-3` up, at most 100 either way. The mouse comes onto the element as it does for `press`, the notches go to the element's window only, and the cursor goes back afterwards. It prints `Scrolled <name> down <n>` or `up <n>`. The driver proves only the refusal for a name that matches no element, which the shell gives before it moves the cursor.

## Sub-features

- `scroll-down` turns the wheel down one notch for each positive step.
- `scroll-up` turns the wheel up one notch for each negative step.
- `scroll-back` puts the cursor back afterwards, and says so when it couldn't because the mouse moved.
- `scroll-window` searches only the window that `[window]` names, by its content type or its number in `tree`.
- `scroll-steps` exits with 2 for a number of steps that is 0, not a whole number, or beyond 100 either way.
- `scroll-in-use` refuses and sends nothing while someone has used the mouse or keyboard in the last 3 seconds, or when another window is on top at the element.
- `scroll-missing` refuses a name that matches no element, before it moves the cursor.

## How to get to it (user POV)

- Run `winshell inspect scroll <name> <steps> [window]` in a terminal, such as `winshell inspect scroll <name> -3` to scroll up three notches.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>`. With none, the command acts on the only running shell.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- No element in the feature shell is named `features.missing`.

- **Refuse a missing element.** Scroll three notches over an element the shell does not have. Run `winshell inspect --pid {pid} scroll features.missing 3`. It exits with 1 and says `No element is named features.missing`, and the wheel does not turn.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "scroll", "features.missing", "3"], "exit": 1, "output": "No element is named features\\.missing"}
]
```

## Gotchas

- This feature does not prove that the wheel turns. A real `scroll` moves the user's cursor, and the driver sends no real input. Do not report `scroll-down` or `scroll-up` as verified.
- The steps come before the window: `scroll <name> <steps> [window]`.
- The notches go to the element's window only, so an element in a menu or flyout in a window of its own does not get them.
- The shell looks up the name before it checks for use of the PC, so this refusal comes even while someone moves the mouse.
- Run real input only in a live session that nobody else is using, never at someone's active desktop.
