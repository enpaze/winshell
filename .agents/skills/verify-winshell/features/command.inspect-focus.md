# winshell inspect focus

`winshell inspect focus` presses Tab in an element's window until keyboard focus is on the element or in it, so its focus rectangle shows, and prints how many Tabs it took, such as `Focused <name> with 3 Tabs`. It is real input, so the shell's handlers for a real Tab run. The driver proves only the refusal for a name that matches no element, which the shell gives before it sends any key.

## Sub-features

- `focus-tab` presses Tab in the element's window until keyboard focus reaches the element, and prints the number of Tabs.
- `focus-round` refuses when Tab goes all the way round without reaching the element, as it does for a panel or text.
- `focus-window` searches only the window that `[window]` names, by its content type or its number in `tree`.
- `focus-in-use` refuses and sends nothing while someone has used the mouse or keyboard in the last 3 seconds.
- `focus-missing` refuses a name that matches no element, before it sends any input.

## How to get to it (user POV)

- Run `winshell inspect focus <name> [window]` in a terminal, where `<name>` is an `x:Name`, an automation id or an automation name.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>`. With none, the command acts on the only running shell.
- After `focus`, `winshell inspect tree` marks the focused element with `"focused": true`, and `winshell inspect key` sends keys to it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- No element in the feature shell is named `features.missing`.

- **Refuse a missing element.** Ask for focus on an element the shell does not have. Run `winshell inspect --pid {pid} focus features.missing`. It exits with 1 and says `No element is named features.missing`, and no Tab is pressed.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "focus", "features.missing"], "exit": 1, "output": "No element is named features\\.missing"}
]
```

## Gotchas

- This feature does not prove that Tab moves focus. A real `focus` presses keys on the desktop, and the driver sends no real input. Do not report `focus-tab` as verified.
- The shell looks up the name before it checks for use of the PC, so this refusal comes even while someone moves the mouse. A real `focus` would then refuse with `Someone used the mouse or keyboard in the last 3 seconds`.
- Only one real input command runs at a time. Another one that is still running makes `focus` refuse with `Another real input command is still running. Nothing was sent.`
- Run real input only in a live session that nobody else is using, never at someone's active desktop.
- Keyboard focus reaches an enabled control with `IsTabStop`, such as a button. A `TextBlock` or a panel never gets it, so `focus` refuses there.
