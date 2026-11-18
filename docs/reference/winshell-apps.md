# Winshell.Apps

Installed apps, search, jump lists and recent documents. Reach them from `Shell.Current.Apps`, `Shell.Current.JumpLists` and `Shell.Current.RecentDocuments`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## AppEntry

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AppId` | string, read-only |
| `Arguments` | string, read-only |
| `CommandLine` | string, read-only |
| `Description` | string, read-only |
| `Folders` | IReadOnlyList of string, read-only |
| `Hidden` | bool, read-only |
| `Icon` | string, read-only |
| `Id` | string, read-only |
| `LastUsed` | long, read-only |
| `Name` | string, read-only |
| `Packaged` | bool, read-only |
| `RunCount` | int, read-only |
| `StartIn` | string, read-only |
| `Target` | string, read-only |
| `Launch()` | void |

## AppJumpList

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Available` | bool, read-only |
| `Destinations` | IReadOnlyList of string, read-only |

## AppsService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `Apps` | ObservableList of [AppEntry](#appentry), read-only |
| `Frequent` | ObservableList of [AppEntry](#appentry), read-only |
| `Recent` | ObservableList of [AppEntry](#appentry), read-only |
| `ById(string id)` | [AppEntry](#appentry)? |
| `DisplayName(string appId)` | string, static |
| `Dispose()` | void |
| `FallbackName(string appId)` | string, static |
| `Find(string name)` | [AppEntry](#appentry)? |
| `ProgramDescription(string path)` | string?, static |
| `Score(string query, string name)` | int, static |
| `Search(string query)` | IReadOnlyList of [AppEntry](#appentry) |

## JumpListService

| Member | Type |
| --- | --- |
| `ForApp(string appId)` | [AppJumpList](#appjumplist) |

## RecentDocument

| Member | Type |
| --- | --- |
| `RecentDocument(string Name, string Path)` | constructor |
| `Name` | string, init |
| `Path` | string, init |

## RecentDocuments

| Member | Type |
| --- | --- |
| `Search(string query)` | IReadOnlyList of [RecentDocument](#recentdocument) |
