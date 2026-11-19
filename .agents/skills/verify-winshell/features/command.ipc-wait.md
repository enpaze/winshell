# winshell ipc wait

`winshell ipc wait <target> <signal>` waits until a running shell raises a signal once, prints the signal's first argument, and exits. On the `features` shell, the signal `ticked` comes once a second with a count, so the command returns within about a second and prints that count.

## Sub-features

- `wait-once` waits for the next time the shell raises the signal, prints its first argument, and exits with 0.
- `wait-refuse` exits with 1 for a target or signal the shell did not register, with `Target not found: <target>.` or `Signal not found: <target>.<signal>.`
- `wait-missing` says what is missing and exits with 2 when the target or the signal is left out.

## How to get to it (user POV)

- Run `winshell ipc wait <target> <signal>` in a terminal, such as in a script that must wait for a shell to raise a signal.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>` before `wait`. With none, the command acts on the only running shell.
- A shell registers a signal with `shell.Ipc.Signal("<target>", "<signal>")` and raises it with `Emit(value)` on the `IpcSignal` that returns.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Wait for one tick.** Wait for the feature shell's next `ticked` signal. Run `winshell ipc --pid {pid} wait features ticked`. It exits with 0 and prints one line, a whole number: the count that signal carried.

```json
[
  {"winshell": ["ipc", "--pid", "{pid}", "wait", "features", "ticked"], "output": "^[0-9]+$"}
]
```

## Gotchas

- `wait` has no time limit. For a signal that never fires, it waits until you press Ctrl+C or the shell exits.
- A signal raised before `wait` starts does not count. The number printed is the next one, not 1.
- A signal raised with no argument prints nothing, and the command still exits with 0.
- Target and signal names are case-sensitive.
