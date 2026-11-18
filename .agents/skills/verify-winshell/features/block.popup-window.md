# PopupWindow

A `PopupWindow` is a frameless window that a shell places next to another, such as a menu or a panel that opens from a bar. In the `features` shell, the `Popup` button opens one, 240 by 32 logical pixels, at the top left of the main window. It shows its real size, such as `popup window: 240x32`.

## Sub-features

- `place` opens the popup at `Rect` on its screen when `Visible` is set to true.
- `grab-focus` gives the popup the keyboard with `GrabFocus`.
- `dismiss` closes a popup that grabs focus when the user clicks outside it or presses Esc, and raises `Dismissed`.
- `content` shows any WinUI element as its `Content`, such as a widget's `context.StylePopup(content)`.

## How to get to it (user POV)

- In a configuration, make `new PopupWindow { Content = ... }`, then set `Rect` and `Visible = true` when the user opens it.
- In a widget, give it `context.StylePopup(content)` so it takes the area's style.
- In the `features` shell, choose the `Popup` button, whose automation id is `features.popup`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Open the popup.** Choose `Popup` in the feature window. Run `winshell inspect --pid {pid} click features.popup`. It exits with 0 and prints `Clicked features.popup`.
- **Read the popup's size.** Look at the popup that opened. Run `winshell inspect --pid {pid} tree`. The first window whose kind is `PopupWindow` holds exactly one element with the automation id `block.popup-window`, and its `text` gives a width and height above zero, matching `^popup window: [1-9][0-9]*x[1-9][0-9]*$`.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "click", "features.popup"], "output": "^Clicked features\\.popup$"},
  {"element": "block.popup-window", "window": "PopupWindow", "has": {"text": "^popup window: [1-9][0-9]*x[1-9][0-9]*$"}}
]
```

## Gotchas

- `inspect click` invokes the button through its automation peer. It sends no real input and does not move the user's pointer.
- `tree` lists only open windows. Before the click, no `PopupWindow` is in the tree, so the second step fails when the first one did.
- The feature popup does not grab focus, so a click outside it and Esc leave it open. It stays until the shell ends.
- The step proves the popup opened with a size. It does not check its place next to the main window or dismissal.
- The panel window and the popup both show a `Row`, so `inspect screenshot <file> Row` saves the panel. Name the popup by its `window` number in the tree, such as `inspect --pid {pid} screenshot popup.png 2`.
