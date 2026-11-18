# Wallpaper

`shell.Wallpaper` reads and sets the desktop wallpaper that Windows draws: the image of each screen, the fit and the background color. A configuration uses it for a wallpaper picker. The feature shell's row `service.wallpaper` shows `fit: <fit>; screens: N`, where the fit is a `WallpaperFit` value: `Center`, `Tile`, `Stretch`, `Fit`, `Fill` or `Span`. The feature shell only reads it.

## Sub-features

- `screens` `Screens` lists each `WallpaperScreen`, with its `Id`, its `Image` and whether it `IsPrimary`.
- `fit` `Fit` is how the image fills the screen, and `SetFit(fit)` changes it.
- `background` `Background` is the color behind the image, and `SetBackground(color)` changes it.
- `image` `SetImage(image)` sets one image on every screen, and `SetImage(screen, image)` sets the image of one screen.
- `refused` each setter returns `false` when nothing changes or Windows refuses it.
- `update` the values follow a change made outside the shell, such as in Windows Settings.

## How to get to it (user POV)

- A configuration reads `shell.Wallpaper`, or `Props.Shell.Wallpaper` in a widget.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the wallpaper.** Read the `service.wallpaper` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.wallpaper`, and its `text` matches `^fit: [A-Za-z]+; screens: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.wallpaper", "has": {"text": "^fit: [A-Za-z]+; screens: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- The setters change the user's real wallpaper. No step calls them, so the map proves only the read.
- A setter returns `false` when the value is already in use. That is not a failure.
- The fit and the images depend on the user's choices, so the step accepts any fit and any number of screens.
