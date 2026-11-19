# winshell use

`winshell use <name>` chooses the shell in `<name>` under the shells folder as the one that `winshell run` starts with no `-p` or `-s`, and so the sign-in entry and the Start tile too. It builds that shell first when it needs a build, records the name in `chosen.txt` in the shells folder, and prints `Chose <name>.` When other shells run, it stops each one, prints `Stopped <id>`, starts the chosen one in the background, and prints `Started <name>`. `winshell use` with no name lists the shells, one a line, with `*` before the one `winshell run` would start. On the `features` shell, the shells folder is `features`, and its shell keeps running when it is chosen.

## Sub-features

- `use-choose` records `<name>` as the shell that `winshell run`, the sign-in entry and the Start tile start, and prints `Chose <name>.`
- `use-build` builds the shell first when it has no build or a source is newer. A build that fails prints why, exits with 1 and changes nothing.
- `use-switch` stops every other running shell and starts the chosen one with `--no-build`. A running shell of the chosen shell stays.
- `use-list` lists the shells in the shells folder, with `*` before the one `winshell run` would start, and before none when it would start none.
- `use-empty` prints `No shells in <folder>. Run winshell new to make one.` when there are none.
- `use-refuse` prints `winshell use needs the name of a shells folder, not "<name>".` and exits with 2 for a name that is not one folder name, such as `..\other`. A shell that does not stop leaves the choice made, the chosen one not started, and exits with 1.

## How to get to it (user POV)

- Run `winshell use <name>` in a terminal, such as `winshell use starter`, to choose a shell and switch to it.
- Run `winshell use` to list the shells and see which one is chosen.
- `winshell doctor` names `winshell use <name>` when it can't tell which shell to start, or when the sign-in entry starts another.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's project is in the session's shells folder as `features`.

- **Choose features.** Choose the feature shell. Run `winshell use features`. It exits with 0 and prints `Chose features.` on a line of its own.
- **List the shells.** List the shells. Run `winshell use`. It exits with 0, and the line for `features` reads `* features`.

```json
[
  {"winshell": ["use", "features"], "output": "^Chose features\\.$"},
  {"winshell": ["use"], "output": "^\\* features$"}
]
```

## Gotchas

- `use` writes `chosen.txt` in the shells folder and stops every other running shell. The session sets `WINSHELL_CONFIG_DIR` and `WINSHELL_LOCAL_DIR`, so it changes the session's choice and shells only. Run without them, it changes what the user's sign-in starts and stops the user's shell.
- `features` here is a shells folder name. It is also the name of the feature shell's IPC target, which `use` does not read.
- The `features` shell is the chosen shell's own shell, so it keeps running, and the command prints neither `Stopped` nor `Started`.
- `WINSHELL_CONFIG_DIR` applies at sign-in only when it is set for the user account.
