# winshell ipc get

`winshell ipc get <target> <property>` prints the current value of a property that a running shell registered with `shell.Ipc.Property`. The shell reads the value each time the command asks. On the `features` shell, the target `features` has the property `name`, whose value is `features`.

## Sub-features

- `get-print` prints the property's current value on one line, a `bool` as `true` or `false` and a color as `#aarrggbb`.
- `get-refuse` exits with 1 for a target or property the shell did not register, with `Target not found: <target>.` or `Property not found: <target>.<property>.`
- `get-missing` says what is missing and exits with 2 when the target or the property is left out.

## How to get to it (user POV)

- Run `winshell ipc get <target> <property>` in a terminal.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>` before `get`. With none, the command acts on the only running shell.
- A shell registers a property with `shell.Ipc.Property("<target>", "<property>", () => value)`, and disposes the registration to take it away.
- `winshell ipc show` lists the properties that `ipc get` can read, with their types.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Read the name.** Read the feature shell's `name` property. Run `winshell ipc --pid {pid} get features name`. It exits with 0 and prints `features` on a line of its own.

```json
[
  {"winshell": ["ipc", "--pid", "{pid}", "get", "features", "name"], "output": "^features$"}
]
```

## Gotchas

- `features` is the name of the shell, of its shells folder, of the IPC target and of the property's value. The printed `features` is the value of `name`, not the name of the target.
- Target and property names are case-sensitive.
- `ipc show` leaves out a property of a type other than `string`, `int`, `bool`, `real`, `double` or `color`, and a shell can't register a property of a type IPC can't pass.
- The CLI and the shell must come from the same release, or each refuses the other.
