# winshell inspect screenshot

`winshell inspect screenshot` saves a window of a running shell as a PNG at its real size in pixels, margins included, and prints its size and the file, as `<width>x<height> <file>`. The shell draws the picture itself, so nothing over the window gets in and the user's pointer and focus stay where they are. A backdrop such as Mica, which the system draws, becomes the plain dark or light color of the theme. On the `features` shell, the driver saves the `Winshell features` window, whose content type is `FeatureWindow`.

## Sub-features

- `screenshot-window` saves the window that `[window]` names, by its content type or its number in `tree`, or the first window without one.
- `screenshot-popup` saves an open popup when `[window]` names the type of its content, such as `MenuFlyoutPresenter`.
- `screenshot-backdrop` puts the window on the plain dark or light color of the theme instead of its backdrop.
- `screenshot-size` prints the picture's width and height in pixels, and the full path of the file.

## How to get to it (user POV)

- Run `winshell inspect screenshot <file> [window]` in a terminal.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>`. With none, the command acts on the only running shell.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Save the feature window.** Save the window that shows `FeatureWindow` as a PNG. Run `winshell inspect --pid {pid} screenshot {scratch}/window.png FeatureWindow`. It exits with 0, its output starts with the picture's width and height, such as `<width>x<height> `, and `{scratch}/window.png` exists and is not empty.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "screenshot", "{scratch}/window.png", "FeatureWindow"], "output": "^[0-9]+x[0-9]+ ", "file": "{scratch}/window.png"}
]
```

## Gotchas

- The step checks that a PNG exists, not what it shows. Open the PNG and look at it before you call a visual change verified.
- The size is in pixels, so it changes with the screen's scale. Do not expect it to equal the window's size in `tree`.
- A relative file is taken from the folder you run `winshell` in. The folder must already exist.
- Without `[window]`, the command saves the first window in `tree` order, which may be a panel and not the window you want.
- `screenshot` without a file exits with 2 and says `winshell inspect screenshot needs a file.`
