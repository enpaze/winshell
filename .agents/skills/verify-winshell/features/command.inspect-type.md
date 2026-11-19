# winshell inspect type

`winshell inspect type` replaces the text in a text box of a running shell, and prints `Typed into <name>`. The box raises `TextChanged` as it does when the user types. It sends no keys, so the user's focus and keyboard stay as they are. On the `features` shell, the box `features.text` shows what it holds in `features.typed`, as `typed: <text>`.

## Sub-features

- `type-text` replaces the whole text of a text box.
- `type-window` searches only the window that `[window]` names, by its content type or its number in `tree`.
- `type-refuse` refuses an element that is not a text box, a read-only or disabled box, a name that matches nothing, and a name that matches more than one element.

## How to get to it (user POV)

- Run `winshell inspect type <name> <text> [window]` in a terminal, where `<name>` is an `x:Name`, an automation id or an automation name. Quote text with spaces.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>`. With none, the command acts on the only running shell.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Type in the box.** Put `hello` in the feature window's text box. Run `winshell inspect --pid {pid} type features.text hello`. It exits with 0 and prints `Typed into features.text`.
- **Read what it holds.** Read the text next to the box. Run `winshell inspect --pid {pid} tree`. Exactly one element is named `features.typed`, and its `text` is `typed: hello`.
- **Type in a button.** Try to type in the `Click` button. Run `winshell inspect --pid {pid} type features.click hello`. It exits with 1 and says that `features.click` is a `Button`, which takes no text.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "type", "features.text", "hello"], "output": "^Typed into features\\.text$"},
  {"element": "features.typed", "has": {"text": "^typed: hello$"}},
  {"winshell": ["inspect", "--pid", "{pid}", "type", "features.click", "hello"], "exit": 1, "output": "features\\.click is a Button, which takes no text\\."}
]
```

## Gotchas

- `type` sets the value at once. `KeyDown`, `CharacterReceived` and other key handlers do not run. Use `winshell inspect key` in a live session for those.
- It replaces the whole text and does not move keyboard focus to the box.
- The CLI and the shell must come from the same release. Otherwise each refuses the other, so build the shell with the `winshell` you drive it with.
