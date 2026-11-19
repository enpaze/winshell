# Winshell.Notifications

Notification Center, Focus, and toasts. Reach them from `Shell.Current.Notifications`.

<!-- generated below: dotnet run scripts/generate-reference.cs -->

## Notification

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AppIcon` | string, read-only |
| `AppId` | string, read-only |
| `AppName` | string, read-only |
| `Body` | string, read-only |
| `CreatedAt` | DateTimeOffset, read-only |
| `ExpiresAt` | DateTimeOffset?, read-only |
| `Id` | uint, read-only |
| `Title` | string, read-only |
| `Dismiss()` | bool |

## NotificationAccessStatus

| Value | Number |
| --- | --- |
| `Unspecified` | 0 |
| `Allowed` | 1 |
| `Denied` | 2 |
| `Unavailable` | 3 |
| `Requesting` | 4 |

## NotificationGroup

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AppId` | string, read-only |
| `AppName` | string, read-only |
| `Notifications` | ObservableList of [Notification](#notification), read-only |
| `Dismiss()` | bool |

## NotificationMode

| Value | Number |
| --- | --- |
| `Unknown` | -1 |
| `Unrestricted` | 0 |
| `PriorityOnly` | 1 |
| `AlarmsOnly` | 2 |

## NotificationState

| Value | Number |
| --- | --- |
| `Unknown` | 0 |
| `Off` | 1 |
| `On` | 2 |

## NotificationsService

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `AccessStatus` | [NotificationAccessStatus](#notificationaccessstatus), read-only |
| `DoNotDisturbState` | [NotificationState](#notificationstate), read-only |
| `Error` | string, read-only |
| `FocusState` | [NotificationState](#notificationstate), read-only |
| `Groups` | ObservableList of [NotificationGroup](#notificationgroup), read-only |
| `NotificationMode` | [NotificationMode](#notificationmode), read-only |
| `Notifications` | ObservableList of [Notification](#notification), read-only |
| `NotificationAdded` | Action of [Notification](#notification) |
| `OperationFailed` | Action of string |
| `ClearAll()` | bool |
| `RequestAccess()` | void |

## ToastAction

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `ToastAction()` | constructor |
| `ActivationUri` | string |
| `Text` | string |

## ToastMessage

Has the members of [ObservableObject](winshell.md#observableobject).

| Member | Type |
| --- | --- |
| `ToastMessage()` | constructor |
| `Actions` | ObservableList of [ToastAction](#toastaction), read-only |
| `ActivationUri` | string |
| `AppId` | string |
| `Body` | string |
| `Error` | string, read-only |
| `Group` | string |
| `Silent` | bool |
| `Tag` | string |
| `Title` | string |
| `Failed` | Action of string |
| `Send()` | bool |
