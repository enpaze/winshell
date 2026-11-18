# winshell ipc show

`winshell ipc show` lists a running shell's IPC targets with their functions, properties and signals. With `-j` it prints a JSON array, one object per target in ordinal order of the target name, each with `target`, `functions`, `properties` and `signals`. Members stay in the order the configuration registered them. On the `features` shell, the one target is `features`, with the function `echo`, the property `name` and the signal `ticked`.

## Sub-features

- `show-list` lists each target with its functions, properties and signals.
- `show-json` prints the list as JSON with `-j` or `--json`. A function has `name`, `parameters` and `returns`, a property has `name` and `type`, and a signal has `name` and `parameters`.
- `show-reachable` leaves out a function that `ipc call` cannot reach, and a property of a type other than `string`, `int`, `bool`, `real`, `double` or `color`.

## How to get to it (user POV)

- Run `winshell ipc show` or `winshell ipc show -j` in a terminal.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-c <name>` or `-p <path>` before `show`. With none, the command acts on the only running shell.
- A configuration adds targets with `shell.Ipc.Handle`, `shell.Ipc.Property` and `shell.Ipc.Signal`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **List the targets.** Ask the feature shell for its IPC targets as JSON. Run `winshell ipc --pid {pid} show -j`. It exits with 0, and the array has an object with `"target": "features"`.

```json
[
  {"winshell": ["ipc", "--pid", "{pid}", "show", "-j"], "output": "\"target\": \"features\""}
]
```

## Gotchas

- The step checks only the target's name, not its members. Read the JSON in `steps.txt` to see `echo`, `name` and `ticked`.
- `winshell ipc call notice dismiss` works, but `notice` is Winshell's own and not a target the configuration registered, so `ipc show` does not list it.
- A target exists only while it has a member. Disposing a target's last registration takes the target off the list.
- The CLI and the shell must come from the same release, or each refuses the other.
