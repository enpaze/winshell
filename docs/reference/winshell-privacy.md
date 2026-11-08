# Winshell.Privacy

Which apps are using the microphone, the camera or location right now. Reach it from `Shell.Current.Privacy`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## PrivacyApp

An app using the microphone, the camera or location right now.

| Member | Type | Description |
| --- | --- | --- |
| `Name` | string, read-only | The name to show for the app. |
| `Packaged` | bool, read-only | Whether this is a packaged app. A packaged app has no path. |
| `Path` | string, read-only | The desktop program's path. Empty for a packaged app. |
| `ThisProgram` | bool, read-only | Whether this is the program the shell is running as. The shell's audio meter holds a microphone stream open, and Windows counts that as microphone use; this is how a configuration tells that use from another app's. |

## PrivacyService

Which apps are using the microphone, the camera or location right now. Windows records each in the capability consent store, and a microphone is also in use while a recording stream is open: current Windows does not always write that stream to the store, and the shell's own audio meter is one such stream. Nothing is read while nothing listens. The first handler starts reading, and removing the last stops it. A change is raised on the loop's thread a tenth of a second after Windows reports it, so one use is one notice. Add and remove handlers on the loop's thread.

| Member | Type | Description |
| --- | --- | --- |
| `Camera` | IReadOnlyList of [PrivacyApp](#privacyapp), read-only | The apps using the camera. Empty while nothing listens, and while none is. |
| `CameraInUse` | bool, read-only | Whether any app is using the camera. False while nothing listens. |
| `Location` | IReadOnlyList of [PrivacyApp](#privacyapp), read-only | The apps using location. Empty while nothing listens, and while none is. |
| `LocationInUse` | bool, read-only | Whether any app is using location. False while nothing listens. |
| `Microphone` | IReadOnlyList of [PrivacyApp](#privacyapp), read-only | The apps using the microphone. Empty while nothing listens, and while none is. |
| `MicrophoneInUse` | bool, read-only | Whether any app is using the microphone. False while nothing listens. |
| `PropertyChanged` | PropertyChangedEventHandler | Raised on the loop's thread when one of the lists or its in-use flag changes. The first handler added starts reading Windows, and removing the last stops it. Add and remove handlers on the loop's thread. |
| `Dispose()` | void | Stops reading Windows, and a handler added later does not start it again. |
