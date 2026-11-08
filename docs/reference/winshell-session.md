# Winshell.Session

The Windows session, and running at sign-in. Reach them from `Shell.Current.Session` and `Shell.Current.Startup`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## SessionService

The signed-in Windows session the shell runs in. Lock, SignOut, Restart, ShutDown, Sleep and Hibernate ask Windows, and Windows' own screens and prompts take over. Winshell never draws a lock screen, asks for the Windows password, or confirms the action. A configuration that wants a confirmation asks for it itself.

| Member | Type | Description |
| --- | --- | --- |
| `HibernateAvailable` | bool, read-only | Whether hibernate is turned on for this PC, so a menu can hide Hibernate when it cannot. |
| `SleepAvailable` | bool, read-only | Whether this PC can sleep, including Modern Standby, so a menu can hide Sleep when it cannot. |
| `Hibernate()` | bool | Asks Windows to hibernate. Returns whether Windows took the request. HibernateAvailable is false when hibernate is turned off. |
| `Lock()` | bool | Asks Windows to lock the session, as Win+L does. Returns whether Windows took the request; the lock itself happens a moment later, in Windows. |
| `Restart()` | bool | Asks Windows to restart the PC. Apps are not forced closed. Registered apps come back after sign-in only when the user has that setting on. Returns whether Windows took the request. |
| `ShutDown()` | bool | Asks Windows to shut the PC down and power it off, requesting fast startup so that setting can apply. Apps are not forced closed. Returns whether Windows took the request. |
| `SignOut()` | bool | Asks Windows to sign out of the session. An app with unsaved work can still hold it up, and Windows shows its own prompt. Apps come back after the next sign-in only when the user has that setting on. Returns whether Windows took the request. |
| `Sleep()` | bool | Asks Windows to sleep, on a Modern Standby PC or one that sleeps with S3. Returns whether Windows took the request. |

## StartupService

Whether this configuration runs when the current user signs in. An unpackaged Winshell is a `Winshell` value under the user's `Run` key; the value starts this install's `winshell.exe` running this configuration. A packaged Winshell is the package's startup task. Windows' Startup apps page lists either and can turn it off. Nothing is registered until the shell sets RunAtSignIn, and Winshell never changes what that page chose.

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type | Description |
| --- | --- | --- |
| `Available` | bool, read-only | Whether this shell can register itself to run at sign-in. False when a packaged shell has no startup task, and false when this process is a configuration program that no `winshell.exe` is supervising. |
| `RegisteredElsewhere` | bool, read-only | True when the entry is there but starts another install or another configuration: a `winshell.exe` in another folder, as after an update unzipped somewhere new, or a different configuration. RunAtSignIn is false until it is set. |
| `RunAtSignIn` | bool | Whether this configuration is registered to run at sign-in. Setting it adds or removes the entry, and setting it when the entry starts another install or another configuration points the entry here: this install's `winshell.exe run` of this configuration. |
| `TurnedOffInWindows` | bool, read-only | True when the entry is there but Windows' Startup apps page turned it off. Only that page can turn it back on. |
| `Refresh()` | void | Reads the entry again, after the user may have changed it in Windows. |
