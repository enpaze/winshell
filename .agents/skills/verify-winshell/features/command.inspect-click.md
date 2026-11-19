# winshell inspect click

`winshell inspect click` activates an element of a running shell through its UI Automation peer, as Narrator does: it invokes a button, toggles a toggle and opens what expands, and prints `Clicked <name>`. It sends no mouse or keyboard input, so the user's pointer and focus stay where they are. On the `features` shell, the `Click` button `features.click` raises the count that `features.clicks` shows, as `clicks: <n>`.

## Sub-features

- `click-invoke` invokes a button, so its `Click` handler runs.
- `click-toggle` toggles a control that toggles, such as a toggle button or a check box.
- `click-expand` opens a control that expands, such as a combo box.
- `click-window` searches only the window that `[window]` names, by its content type or its number in `tree`.
- `click-refuse` refuses a disabled control, an element that takes no click, a name that matches nothing, and a name that matches more than one element.

## How to get to it (user POV)

- Run `winshell inspect click <name> [window]` in a terminal, where `<name>` is an `x:Name`, an automation id or an automation name.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>`. With none, the command acts on the only running shell.
- A shell gives an element a name to click with `AutomationId(...)`, `x:Name` or `AutomationName`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open and its count shows `clicks: 0`.

- **Click the button.** Activate the `Click` button through its automation peer. Run `winshell inspect --pid {pid} click features.click`. It exits with 0 and prints `Clicked features.click`.
- **Read the count.** Read the count next to the button. Run `winshell inspect --pid {pid} tree`. Exactly one element is named `features.clicks`, and its `text` reads `clicks: ` and a whole number of 1 or more.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "click", "features.click"], "output": "^Clicked features\\.click$"},
  {"element": "features.clicks", "has": {"text": "^clicks: [1-9][0-9]*$"}}
]
```

## Gotchas

- `click` runs the control's automation action, not a real mouse press. A `PointerPressed` handler and the pressed look do not run. Use `winshell inspect press` for those.
- Each run adds one to the count, so the count is 1 only on a freshly started shell. The step accepts any count from 1.
- A name finds only elements that are not collapsed. A name that matches more than one element acts on none, and the error lists each match with its window number. Add the window number to pick one.
- An element that has no invoke, toggle or expand action, such as a `TextBlock`, refuses with `<name> is a <type>, which takes no click.` and exits with 1.
- The CLI and the shell must come from the same release. Otherwise each refuses the other, so build the shell with the `winshell` you drive it with.
