# winshell inspect hover

`winshell inspect hover` puts a control of a running shell in its `PointerOver` look, the look it has when a pointer is over it, and prints `Hovering <name>`. It sends no mouse input, so the user's pointer stays where it is, and a `PointerEntered` handler does not run. On the `features` shell, the `Click` button `features.click` takes its hover look.

## Sub-features

- `hover-look` puts a control in its `PointerOver` visual state.
- `hover-window` searches only the window that `[window]` names, by its content type or its number in `tree`.
- `hover-refuse` refuses an element that has no hover look, a name that matches nothing, and a name that matches more than one element.

## How to get to it (user POV)

- Run `winshell inspect hover <name> [window]` in a terminal, where `<name>` is an `x:Name`, an automation id or an automation name.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-c <name>` or `-p <path>`. With none, the command acts on the only running shell.
- Take `winshell inspect screenshot` afterwards to see the hover look.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open with its `Click` button.

- **Hover the button.** Put the `Click` button in its hover look. Run `winshell inspect --pid {pid} hover features.click`. It exits with 0 and prints `Hovering features.click`.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "hover", "features.click"], "output": "^Hovering features\\.click$"}
]
```

## Gotchas

- The step proves that the button entered its `PointerOver` state, not how that look is drawn. To see the look, take a screenshot after `hover` and look at it.
- `hover` runs no `PointerEntered` handler, because XAML has no way to raise one from code. To check code that runs on a pointer, use `winshell inspect point`, which is real input.
- Only a control has a hover look. An element such as a `TextBlock` refuses with `<name> is a <type>, which has no hover look.` and exits with 1.
- A name that matches more than one element acts on none. The error lists each match with its window number.
