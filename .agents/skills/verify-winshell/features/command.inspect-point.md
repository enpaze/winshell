# winshell inspect point

`winshell inspect point` moves the real mouse onto the middle of an element of a running shell, so `PointerEntered` and the `PointerOver` look run, and prints `Pointing at <name>. winshell inspect leave puts the cursor back.` The mouse stays there until `leave`, the next real input command, 30 seconds, or a move of the mouse. The driver proves only the refusal for a name that matches no element, which the shell gives before it moves the cursor.

## Sub-features

- `point-hold` moves the cursor onto the middle of the element and holds it there, so `PointerEntered` and the hover look run.
- `point-end` ends the hold at `leave`, at the next real input command, after 30 seconds, or when the mouse moves.
- `point-window` searches only the window that `[window]` names, by its content type or its number in `tree`.
- `point-covered` refuses when another window is on top at the element's middle, or the element is clipped, covered, not hit-testable or has no size.
- `point-in-use` refuses and sends nothing while someone has used the mouse or keyboard in the last 3 seconds, or when the mouse moves while it works.
- `point-missing` refuses a name that matches no element, before it moves the cursor.

## How to get to it (user POV)

- Run `winshell inspect point <name> [window]` in a terminal, where `<name>` is an `x:Name`, an automation id or an automation name.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>`. With none, the command acts on the only running shell.
- Run `winshell inspect leave` afterwards to put the cursor back. Record with `winshell inspect record` in the background to see the hover.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- No element in the feature shell is named `features.missing`.

- **Refuse a missing element.** Point at an element the shell does not have. Run `winshell inspect --pid {pid} point features.missing`. It exits with 1 and says `No element is named features.missing`, and the cursor does not move.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "point", "features.missing"], "exit": 1, "output": "No element is named features\\.missing"}
]
```

## Gotchas

- This feature does not prove that the pointer arrives. A real `point` moves the user's cursor, and the driver sends no real input. Do not report `point-hold` as verified.
- The shell looks up the name before it checks for use of the PC, so this refusal comes even while someone moves the mouse.
- A move of the mouse ends the hold and leaves the cursor where the user put it.
- A flyout or menu that the element's window opened counts as another window on top, so `point` refuses where it covers the element.
- Run real input only in a live session that nobody else is using, never at someone's active desktop.
