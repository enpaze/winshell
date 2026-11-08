# Winshell.Wallpaper

The desktop wallpaper Windows draws: each screen's image, the fit and the background color. Reach it from `Shell.Current.Wallpaper`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## WallpaperColor

The color behind the wallpaper image, and the color of a screen that has no image.

| Member | Type | Description |
| --- | --- | --- |
| `WallpaperColor(byte red, byte green, byte blue)` | constructor | A color of red, green and blue, each from 0 to 255. |
| `Blue` | byte, read-only | Blue, from 0 to 255. |
| `Green` | byte, read-only | Green, from 0 to 255. |
| `Red` | byte, read-only | Red, from 0 to 255. |

## WallpaperFit

How the wallpaper image is fitted to the screen. Every screen uses the same fit.

| Value | Number | Description |
| --- | --- | --- |
| `Center` | 0 | The image keeps its size and sits in the middle of the screen. |
| `Tile` | 1 | The image repeats until it covers the screen. |
| `Stretch` | 2 | The image is stretched to the screen, even when that changes its shape. |
| `Fit` | 3 | The whole image is shown, as large as it can be without cropping or changing its shape. |
| `Fill` | 4 | The image covers the screen without changing its shape, cropping whatever does not fit. |
| `Span` | 5 | One image covers every screen, as large as it can be without changing its shape. |

## WallpaperScreen

The wallpaper on one screen.

| Member | Type | Description |
| --- | --- | --- |
| `Id` | string, read-only | The screen's id, the same one ScreenInfo uses, such as `\\.\DISPLAY1`. A monitor that could not be matched keeps the id Windows uses for its wallpaper. |
| `Image` | string, read-only | The image on this screen, or empty when the screen shows only the background color. |
| `IsPrimary` | bool, read-only | Whether this is the primary screen. |

## WallpaperService

The desktop wallpaper Windows draws. Each screen has its own image. The fit and the background color are shared, and a change stays after the shell exits, because it is Windows' own setting. A slideshow or Windows Spotlight reports the image Windows is showing on that screen, or none when Windows reports no image. This does not manage a slideshow. Windows writes several values for one change and does not broadcast it, so a change made in Settings is read a tenth of a second later and raised on the loop's thread. Until a reading succeeds there are no screens. A later reading that cannot reach Explorer's desktop leaves the last one in place, and a set does nothing.

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type | Description |
| --- | --- | --- |
| `Background` | [WallpaperColor](#wallpapercolor), read-only | The color behind the image, and the color of a screen with no image. The same color is used on every screen. |
| `Fit` | [WallpaperFit](#wallpaperfit), read-only | How the image is fitted. The same fit is used on every screen. |
| `Screens` | IReadOnlyList of [WallpaperScreen](#wallpaperscreen), read-only | Each screen's wallpaper, in the order Windows lists the monitors. Empty when the desktop wallpaper cannot be read. |
| `Dispose()` | void | Stops watching the wallpaper, and a later set does nothing. |
| `SetBackground([WallpaperColor](#wallpapercolor) color)` | bool | Sets the background color every screen uses. The color already in use is ignored. Until a reading succeeds, the color shown is not one Windows reported, so setting it still asks Windows. Returns whether Windows was asked. Call it on the loop's thread. |
| `SetFit([WallpaperFit](#wallpaperfit) fit)` | bool | Sets the fit every screen uses. The fit already in use, and one Windows does not define, are ignored. Until a reading succeeds, the fit shown is not one Windows reported, so setting it still asks Windows. Returns whether Windows was asked. Call it on the loop's thread. |
| `SetImage(string image)` | bool | Sets image on every screen. An empty path, and an image every screen already shows, are ignored. Returns whether Windows was asked. Call it on the loop's thread. image is the full path of an image file. |
| `SetImage([WallpaperScreen](#wallpaperscreen) screen, string image)` | bool | Sets image on screen and leaves the others as they are. An unknown screen, an empty path and the image already there are ignored. Returns whether Windows was asked. Call it on the loop's thread. image is the full path of an image file. |
