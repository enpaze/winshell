# omarchy-style workspaces widget

The `workspaces` widget shows Winland's workspaces for the screen its bar is on, after the Windows glyph at the start of the omarchy-style bar. It shows buttons 1 to 5 always, and any other workspace that exists. An empty workspace is dimmed, the shown one is the tiling glyph, and choosing a button shows that workspace. With the `icons` setting, which the example's default list turns on, each button also shows the icons of the apps open on that workspace, one per app. Without Winland running it shows nothing.

## Sub-features

- `buttons` shows a button for workspaces 1 to 5 and for each other workspace on that screen, named `Workspace <n>`.
- `active` shows the tiling glyph in place of the number of the workspace that is shown.
- `dim` dims a workspace with no windows.
- `activate` shows a workspace when its button is chosen.
- `per-screen` shows each screen's own workspaces on that screen's bar.
- `no-winland` shows nothing when Winland is not running.
- `vertical` stacks the buttons on a side bar.
- `icons` shows, after the number, one 12 pixel icon per app open on that workspace, and an empty workspace shows its number alone. The icons change as windows open, close and move between workspaces. Without `icons = true` on the placement the buttons are text only. `icons` is read as a `bool` preferences key: `true` or a number other than zero turns it on, `false` or `0` leaves it off, and a string makes the area show `workspaces: icons must be true or false.` in the widget's place.

## How to get to it (user POV)

- Run `winshell new omarchy-style` and then `winshell run -c omarchy-style`, or `winshell run --path examples/omarchy-style` from a clone.
- The example registers it with `Widgets.Register<WorkspacesWidget>("workspaces")` and places it in the bar's `Start` list.
- Place it elsewhere with `"workspaces"` in a list of the `[bar]` section of the preferences file, or `{ widget = "workspaces", icons = true }` for the app icons.
- Choose a `Workspace <n>` button at the left of the bar.

## Driving it with winshell inspect

Preconditions:

- Shell: `omarchy-style`.

- **Find the workspaces.** Start the omarchy-style shell and look at the left of the first bar. Run `winshell inspect --pid {pid} tree`. In the first window whose content type is `Surface`, exactly one element has the automation id `workspaces`.

```json
[
  {"element": "workspaces", "window": "Surface"}
]
```

## Gotchas

- The buttons need Winland, which CI lacks. Without it the element is a hidden, empty text block, and the step still passes. It proves only that the widget rendered without failing.
- With Winland running, look for `Workspace 1` to `Workspace 5` in `tree-<n>.json`. For `icons`, take `inspect screenshot` of the bar: a workspace with windows shows its apps' icons after its number, and opening, closing or moving a window there adds or removes its icon. Never use `inspect click` on them: it switches the user's real workspace.
- With Winland running, follow "Live UI with Winland running" in the development guide: never add the window rule or reload Winland.
- The omarchy-style shell hides Explorer's taskbar while it runs and puts it back when it exits.
