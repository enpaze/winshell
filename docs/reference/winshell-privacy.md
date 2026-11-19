# Winshell.Privacy

Which apps are using the microphone, the camera or location right now. Reach it from `Shell.Current.Privacy`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## PrivacyApp

| Member | Type |
| --- | --- |
| `Name` | string, read-only |
| `Packaged` | bool, read-only |
| `Path` | string, read-only |
| `ThisProgram` | bool, read-only |

## PrivacyService

| Member | Type |
| --- | --- |
| `Camera` | IReadOnlyList of [PrivacyApp](#privacyapp), read-only |
| `CameraInUse` | bool, read-only |
| `Location` | IReadOnlyList of [PrivacyApp](#privacyapp), read-only |
| `LocationInUse` | bool, read-only |
| `Microphone` | IReadOnlyList of [PrivacyApp](#privacyapp), read-only |
| `MicrophoneInUse` | bool, read-only |
| `PropertyChanged` | PropertyChangedEventHandler |
