# Winshell.Wallpaper

The desktop wallpaper Windows draws: each screen's image, the fit and the background color. Reach it from `Shell.Current.Wallpaper`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## WallpaperColor

| Member | Type |
| --- | --- |
| `WallpaperColor(byte red, byte green, byte blue)` | constructor |
| `Blue` | byte, read-only |
| `Green` | byte, read-only |
| `Red` | byte, read-only |

## WallpaperFit

| Value | Number |
| --- | --- |
| `Center` | 0 |
| `Tile` | 1 |
| `Stretch` | 2 |
| `Fit` | 3 |
| `Fill` | 4 |
| `Span` | 5 |

## WallpaperScreen

| Member | Type |
| --- | --- |
| `Id` | string, read-only |
| `Image` | string, read-only |
| `IsPrimary` | bool, read-only |

## WallpaperService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Background` | [WallpaperColor](#wallpapercolor), read-only |
| `Fit` | [WallpaperFit](#wallpaperfit), read-only |
| `Screens` | IReadOnlyList of [WallpaperScreen](#wallpaperscreen), read-only |
| `Dispose()` | void |
| `SetBackground([WallpaperColor](#wallpapercolor) color)` | bool |
| `SetFit([WallpaperFit](#wallpaperfit) fit)` | bool |
| `SetImage(string image)` | bool |
| `SetImage([WallpaperScreen](#wallpaperscreen) screen, string image)` | bool |
