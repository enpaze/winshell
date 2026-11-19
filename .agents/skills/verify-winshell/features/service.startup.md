# Startup

`shell.Startup` reads and sets whether the shell starts when the user signs in to Windows. A shell uses it for a "Run at sign-in" switch. The feature shell's row `service.startup` shows `available: <True|False>; run at sign-in: <True|False>`, and the feature shell only reads it.

## Sub-features

- `available` `Available` is `True` when the shell can register to run at sign-in, which needs the `winshell.exe` that supervises the shell, or a packaged Winshell.
- `run-at-sign-in` `RunAtSignIn` says whether the sign-in entry starts this shell, and setting it adds or removes the entry for this install's `winshell.exe run -p` of the shell.
- `turned-off` `TurnedOffInWindows` is `True` when Windows' Startup apps page turned the entry off, and Winshell never changes that.
- `elsewhere` `RegisteredElsewhere` is `True` when the entry starts a Winshell in another folder or another shell, and setting `RunAtSignIn` rewrites it for this one.
- `refresh` `Refresh()` reads the entry again.

## How to get to it (user POV)

- A shell reads and sets `shell.Startup.RunAtSignIn`, or `Shell.Startup` in a widget, such as from a switch.
- `install.ps1 -AutoStart` adds a sign-in entry that starts `winshell.exe` with no shell.
- `winshell doctor` reports the sign-in entry in its Sign-in check.
- The user turns the entry off or on in Windows Settings > Apps > Startup.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Read the sign-in state.** Read the `service.startup` row in the feature shell. Run `winshell inspect --pid {pid} tree`. One element has the automation id `service.startup`, and its `text` matches `^available: (True|False); run at sign-in: (True|False)$`. The driver retries for 20 seconds.

```json
[
  {"element": "service.startup", "has": {"text": "^available: (True|False); run at sign-in: (True|False)$"}, "within": 20}
]
```

## Gotchas

- Setting `RunAtSignIn` writes the user's real sign-in entry, so no step sets it. The map proves only the read.
- A shell started without `winshell run`, such as its program started directly, has `available: False`, and setting `RunAtSignIn` then only writes a `startup` warning to the log.
- `run at sign-in` describes the user's own sign-in entry, which can start another shell. The step accepts both states.
