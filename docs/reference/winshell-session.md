# Winshell.Session

The Windows session, and running at sign-in. Reach them from `Shell.Current.Session` and `Shell.Current.Startup`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## SessionService

| Member | Type |
| --- | --- |
| `HibernateAvailable` | bool, read-only |
| `SleepAvailable` | bool, read-only |
| `Hibernate()` | bool |
| `Lock()` | bool |
| `Restart()` | bool |
| `ShutDown()` | bool |
| `SignOut()` | bool |
| `Sleep()` | bool |

## StartupService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Available` | bool, read-only |
| `RegisteredElsewhere` | bool, read-only |
| `RunAtSignIn` | bool |
| `TurnedOffInWindows` | bool, read-only |
| `Refresh()` | void |
