# Launcher

A `Launcher` is a search box that a hotkey opens over the other windows. Typing searches the apps Start lists and the commands the shell gives it, together, and choosing a result runs it. In the `features` shell its hotkey is `Ctrl+Alt+Shift+F14`, and the `Launcher` button opens it too. The row `block.launcher` reads `launcher: hotkey Ctrl+Alt+Shift+F14; visible: False; opened nothing` until the launcher opens Notepad. Then the feature shell closes Notepad's new window, and the row ends in `opened Notepad, whose window it closed`.

## Sub-features

- `open` shows the search box over the other windows, on the screen with the active window, when its hotkey is pressed or `Show` or `Toggle` is called.
- `search` ranks the apps that match the typed text, as `shell.Apps.Search` does, and puts each command before the first app whose name matches less well. It shows the first eight.
- `choose` moves the selection with Up and Down.
- `run` launches the chosen app, or runs the chosen command, on Enter or a click, and closes the search box.
- `dismiss` closes it on Escape, a click outside it, or its hotkey again.
- `hotkey` takes its hotkey from `hotkey` in the launcher's section of the preferences file, `Hotkey` in code until the user sets one.

## How to get to it (user POV)

- In a shell, add `shell.Add(new Launcher { Hotkey = "Alt+Space", Commands = [new("Lock", () => shell.Session.Lock())] })`. It claims its hotkey when it is added.
- Press its hotkey, or call `Show` or `Toggle`, such as from a button in a bar.
- Set `hotkey` under `[launcher]` in the preferences file to choose another hotkey.
- In the `features` shell, choose the `Launcher` button, whose automation id is `features.launcher`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- Start lists Notepad, as it does on Windows 11 and on the CI runner.

- **Read the launcher's row.** Look at the launcher's row before it opens. Run `winshell inspect --pid {pid} tree`. Exactly one element is named `block.launcher`, and its `text` names the hotkey, says the launcher is hidden, and says it opened nothing.
- **Open the launcher.** Choose `Launcher` in the feature window. Run `winshell inspect --pid {pid} click features.launcher`. It exits with 0 and prints `Clicked features.launcher`.
- **See the empty search box.** Look at the launcher. Run `winshell inspect --pid {pid} tree`. The window whose content type is `LauncherView` holds exactly one element named `launcher.query`, and its `text` is empty.
- **Type a search.** Type `notepad` in the search box. Run `winshell inspect --pid {pid} type launcher.query notepad`. It exits with 0 and prints `Typed into launcher.query`.
- **See Notepad chosen.** Look at the first result. Run `winshell inspect --pid {pid} tree`. In the `LauncherView` window, exactly one element is named `launcher.selected`, and its `label` is `Notepad`.
- **Launch it.** Choose the result. Run `winshell inspect --pid {pid} click launcher.selected`. It exits with 0 and prints `Clicked launcher.selected`.
- **See Notepad open.** Look at the launcher's row. Run `winshell inspect --pid {pid} tree`. The `text` of `block.launcher` says the launcher is hidden and opened Notepad, whose window the feature shell then closed.

```json
[
  {"element": "block.launcher", "has": {"text": "^launcher: hotkey Ctrl\\+Alt\\+Shift\\+F14; visible: False; opened nothing$"}, "within": 20},
  {"winshell": ["inspect", "--pid", "{pid}", "click", "features.launcher"], "output": "^Clicked features\\.launcher$"},
  {"element": "launcher.query", "window": "LauncherView", "has": {"text": "^$"}},
  {"winshell": ["inspect", "--pid", "{pid}", "type", "launcher.query", "notepad"], "output": "^Typed into launcher\\.query$"},
  {"element": "launcher.selected", "window": "LauncherView", "has": {"label": "^Notepad$"}, "within": 20},
  {"winshell": ["inspect", "--pid", "{pid}", "click", "launcher.selected"], "output": "^Clicked launcher\\.selected$"},
  {"element": "block.launcher", "has": {"text": "^launcher: hotkey Ctrl\\+Alt\\+Shift\\+F14; visible: False; opened Notepad, whose window it closed$"}, "within": 30}
]
```

## Gotchas

- The map sends no real input, so the steps open the launcher with the `Launcher` button and never press its hotkey. The row shows which hotkey it has. To see the hotkey open it, press `Ctrl+Alt+Shift+F14` in a live session that a person is watching.
- Enter, Up, Down and Escape are real keys, so the steps do not drive `choose`, `dismiss` or Enter. `inspect click` on `launcher.selected` runs the same result as Enter.
- Opening the launcher takes the keyboard from the window in front.
- The last step needs Notepad's window to open, so it waits up to 30 seconds. On a PC where Notepad restores its tabs, the shell still closes only a window that opened after the launcher did.
- The search box is empty each time the launcher opens. A run that fails after `type` leaves the launcher open until the shell ends.
