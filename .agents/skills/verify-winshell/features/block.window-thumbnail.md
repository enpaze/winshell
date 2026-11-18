# WindowThumbnail

A `WindowThumbnail` is a control that shows a live preview of another app's window, drawn by Windows' window manager rather than captured. In the `features` shell, a 64 by 48 thumbnail beside the capture view previews the shell's own window, which it finds in the window list. Its automation name is `window thumbnail: previews this shell's window`, or `window thumbnail: no window` while the window list has not found it.

## Sub-features

- `preview` shows the `AppWindow` it is given and follows the control's size and place.
- `closed` clears the preview when that window closes.
- `peek` with `Peek` set shows that window on the desktop as Windows' peek does, when the peek setting in Windows is on.

## How to get to it (user POV)

- In a configuration, find the window in `shell.WindowList.Windows` and set `new WindowThumbnail { Window = window, Width = 64, Height = 48 }` in a window's content. In a Reactor component, embed it with `XamlHost`.
- In the `features` shell, the thumbnail is the second picture beside the rows, with the automation id `block.window-thumbnail`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's floating window is open, so the window list finds it.

- **Read the thumbnail.** Wait for the window list to find the feature window. Run `winshell inspect --pid {pid} tree`. Exactly one element has the automation id `block.window-thumbnail`, and its `label` is `window thumbnail: previews this shell's window`, matching `^window thumbnail: previews this shell's window$`. The driver retries for 20 seconds.

```json
[
  {"element": "block.window-thumbnail", "has": {"label": "^window thumbnail: previews this shell's window$"}, "within": 20}
]
```

## Gotchas

- The label says that the thumbnail was given the shell's window. It does not prove that Windows drew the preview. Take a screenshot and look at it for that.
- Windows draws the preview, not XAML, so the tree has no picture inside the element.
- The thumbnail takes no pointer input, and Narrator does not see it. Its automation id and name are on the element only so that `tree` can find it.
- The window list can take a moment to list the shell's own window, so the first tree reads can say `no window`. That is why the step retries for 20 seconds.
