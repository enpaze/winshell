# winshell ipc listen

`winshell ipc listen <target> <signal>` prints the first argument of each signal that a running shell raises, one line each, until you press Ctrl+C. A shell raises a signal with the `Emit` method of the `IpcSignal` that `shell.Ipc.Signal` returns. The `features` shell raises its signal `ticked` once a second, with a count that goes up by one each time.

## Sub-features

- `listen-print` prints the first argument of each signal on a line of its own until Ctrl+C.
- `listen-catch-up` keeps the latest signals when the client falls behind.
- `listen-release` makes the shell release the subscription when the client disconnects, even if the signal never fires.
- `listen-refuse` exits with 1 for a target or signal the shell did not register, with `Target not found: <target>.` or `Signal not found: <target>.<signal>.`
- `listen-missing` says what is missing and exits with 2 when the target or the signal is left out.

## How to get to it (user POV)

- Run `winshell ipc listen <target> <signal>` in a terminal, and press Ctrl+C to stop.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>` before `listen`. With none, the command acts on the only running shell.
- A shell registers a signal with `shell.Ipc.Signal("<target>", "<signal>")` and raises it with `Emit(value)` on the `IpcSignal` that returns.
- `winshell ipc show` lists the signals of each target.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Listen to the ticks.** Listen to the feature shell's `ticked` signal for a few seconds. Run `winshell ipc --pid {pid} listen features ticked`. The driver stops it after 4 seconds. By then it has printed at least two lines, each a whole number (`^[0-9]+\r?\n[0-9]+`).

```json
[
  {"winshell": ["ipc", "--pid", "{pid}", "listen", "features", "ticked"], "for": 4, "output": "^[0-9]+\\r?\\n[0-9]+"}
]
```

## Gotchas

- `listen` never ends by itself. The driver stops it after 4 seconds and checks only its output, not its exit code.
- Only signals raised after `listen` starts are printed, so the first number is the shell's count at that moment, not 1. The count starts again at 1 in each new process of the shell.
- A signal raised with no argument prints nothing, so an empty output does not prove that the signal never fired.
- Target and signal names are case-sensitive.
