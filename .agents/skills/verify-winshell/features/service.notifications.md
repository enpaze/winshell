# Notifications

`shell.Notifications` reads the notifications that Windows shows in Notification Center, with Focus and Do not disturb, dismisses them, and sends toasts. A configuration uses it for a notification list or a Do not disturb indicator. The feature shell's row `service.notifications` shows `access: <status>; notifications: N`, where the status is a `NotificationAccessStatus` value: `Unspecified`, `Allowed`, `Denied`, `Unavailable` or `Requesting`.

## Sub-features

- `access` `AccessStatus` says whether the user allowed the shell to read notifications, and `RequestAccess()` asks Windows for that access.
- `list` `Notifications` and `Groups` list each notification, with its app, title, body and time, once access is allowed.
- `added` `NotificationAdded` raises for each new notification.
- `dismiss` `Notification.Dismiss()`, `NotificationGroup.Dismiss()` and `ClearAll()` remove notifications.
- `focus` `FocusState`, `DoNotDisturbState` and `NotificationMode` read Focus and Do not disturb.
- `toast` `ToastMessage` sends a toast with a title, a body and actions, and toast banners are always Windows' own.

## How to get to it (user POV)

- A configuration reads `shell.Notifications`, or `Props.Shell.Notifications` in a widget.
- A configuration calls `shell.Notifications.RequestAccess()` from a button, and the user allows it when Windows asks.
- A configuration creates `new ToastMessage(shell.Loop)`, sets `Title` and `Body`, and calls `Send()`.
- The user allows or denies access in Windows Settings > Privacy & security > Notifications.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the access and the count.** Read the `service.notifications` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.notifications`, and its `text` matches `^access: [A-Za-z]+; notifications: [0-9]+$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.notifications", "has": {"text": "^access: [A-Za-z]+; notifications: [0-9]+$"}, "within": 20}
]
```

## Gotchas

- The feature shell never calls `RequestAccess()`, so it reads no notifications unless the user allowed access before. Without access, the row shows `notifications: 0`.
- Some Windows setups do not give notification access to a program that is not installed as a package. The feature shell runs unpackaged, so the row can show `access: Unavailable`. The step accepts any status.
- `RequestAccess()` shows a Windows prompt, and `ClearAll()` and `Dismiss()` remove the user's real notifications. No step calls them.
