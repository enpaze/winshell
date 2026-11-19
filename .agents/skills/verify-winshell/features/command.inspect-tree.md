# winshell inspect tree

`winshell inspect tree` prints each open window of a running shell as JSON: its number, kind, place and size, and its live XAML tree. Each element has its type, `x:Name`, automation id and automation name, its position in the window, its size, and what it holds, such as `text`, `value`, `on`, `checked` or `selectedIndex`. What a window's open flyouts and menus show is under `popups`. It reads the shell from inside and sends no input. On the `features` shell, the tree holds the `Winshell features` window with the element `features.clicks`.

## Sub-features

- `tree-windows` lists each open window with `window`, `kind`, `x`, `y`, `width` and `height`.
- `tree-elements` writes each element's `type`, `name`, `automationId`, `label`, position, size and `children`.
- `tree-values` writes what an element holds: `text`, `value`, `on`, `checked`, `selectedIndex`, and `enabled` when it is `false`.
- `tree-focus` marks the element keyboard focus is on with `"focused": true`.
- `tree-hidden` marks an element that is not visible with `"visible": false`.
- `tree-popups` lists what the window's open flyouts and menus show under `popups`.
- `tree-password` leaves a password out.

## How to get to it (user POV)

- Run `winshell inspect tree` in a terminal.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>`. With none, the command acts on the only running shell.
- `winshell list` shows the process id to pass to `--pid`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Print the tree.** Print the shell's windows and elements. Run `winshell inspect --pid {pid} tree`. It exits with 0, and its JSON has `"automationId": "features.clicks"`.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "tree"], "output": "\"automationId\": \"features\\.clicks\""}
]
```

## Gotchas

- Find an element by its `name`, `automationId` or `label`, never by its place in the tree.
- The tree lists a collapsed element with `"visible": false`, but the other `inspect` commands do not find it by name.
- A property an element doesn't have is left out of its object, not written as empty.
- The output is long. Search it, or let an element step find the one element you need.
- Output to a pipe or a file is UTF-8. In PowerShell, set `[Console]::OutputEncoding = [Text.UTF8Encoding]::new()` first, or text outside ASCII reads wrong.
