# Winshell.Keyboard

The keyboard layouts Windows has installed. Reach it from `Shell.Current.KeyboardLayout`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## KeyboardLayout

One keyboard layout Windows has installed.

| Member | Type | Description |
| --- | --- | --- |
| `Id` | string, read-only | The id Windows stores for this layout, such as `00000409`. It does not change with the UI language. |
| `Label` | string, read-only | A short label, such as EN. |
| `Name` | string, read-only | The name Windows shows, such as United States-International. |

## KeyboardLayoutService

The keyboard layouts Windows has installed. Each has a short label, such as EN, and the name Windows shows. The current layout is the foreground window's, so a shell window that does not take the foreground does not change it. Windows tells only the focused window when a layout changes, so while something listens the foreground window's layout is read twice a second, and not at all while nothing does.

| Member | Type | Description |
| --- | --- | --- |
| `Current` | [KeyboardLayout](#keyboardlayout)?, read-only | The layout the foreground window is using, or null when it has none or nothing is listening. |
| `Layouts` | IReadOnlyList of [KeyboardLayout](#keyboardlayout), read-only | The installed layouts, in the order Windows cycles them. Empty while nothing listens. |
| `PropertyChanged` | PropertyChangedEventHandler | Raised on the loop's thread when Layouts or Current changes. The first handler added starts reading Windows, and removing the last stops it. Add and remove handlers on the loop's thread. |
| `Dispose()` | void | Stops reading Windows, and a handler added later does not start it again. |
| `Next()` | bool | Asks the foreground window to use the next installed layout, as Win+Space does. Does nothing when that layout is already in use. Returns whether the window was asked. Call it on the loop's thread. |
| `Use([KeyboardLayout](#keyboardlayout) layout)` | bool | Asks the foreground window to use layout, as the input indicator does. A layout that is not installed is ignored, and so is the one already in use. Returns whether the window was asked. Call it on the loop's thread. |
