# Winshell.Keyboard

The keyboard layouts Windows has installed. Reach it from `Shell.Current.KeyboardLayouts`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## KeyboardLayout

| Member | Type |
| --- | --- |
| `Id` | string, read-only |
| `Label` | string, read-only |
| `Name` | string, read-only |

## KeyboardLayoutService

| Member | Type |
| --- | --- |
| `Current` | [KeyboardLayout](#keyboardlayout)?, read-only |
| `Layouts` | IReadOnlyList of [KeyboardLayout](#keyboardlayout), read-only |
| `PropertyChanged` | PropertyChangedEventHandler |
| `Dispose()` | void |
| `Next()` | bool |
| `Use([KeyboardLayout](#keyboardlayout) layout)` | bool |
