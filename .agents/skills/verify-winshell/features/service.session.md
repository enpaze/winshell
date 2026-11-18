# Session

`shell.Session` asks Windows to lock, sign out, restart, shut down, sleep or hibernate, and says whether sleep and hibernate are available. A configuration uses it for a power menu. The feature shell's row `service.session` shows `sleep: <True|False>; hibernate: <True|False>`, and the feature shell never calls an action.

## Sub-features

- `available` `SleepAvailable` and `HibernateAvailable` say whether this PC can sleep or hibernate.
- `lock` `Lock()` locks the session, and Windows' own lock screen takes over.
- `sign-out` `SignOut()` signs the user out.
- `restart-shut-down` `Restart()` and `ShutDown()` restart or shut down the PC.
- `sleep-hibernate` `Sleep()` and `Hibernate()` put the PC to sleep or hibernate it.
- `refused` each action returns `false` when Windows refuses it, and the log has a `session` warning.

## How to get to it (user POV)

- A configuration calls `shell.Session.Lock()` and the other actions, or `Shell.Session` in a widget, such as from a button.
- The starter and the omarchy-style example do not use it, so no control shows it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read what is available.** Read the `service.session` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.session`, and its `text` matches `^sleep: (True|False); hibernate: (True|False)$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.session", "has": {"text": "^sleep: (True|False); hibernate: (True|False)$"}, "within": 20}
]
```

## Gotchas

- Every action ends or interrupts the user's session. No step calls one, so the map proves only `SleepAvailable` and `HibernateAvailable`.
- Sleep and hibernate depend on the PC and on its power settings, so the step accepts both states.
- The service has no change notice, so the row does not update by itself.
