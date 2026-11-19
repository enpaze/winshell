# winshell list

`winshell list` lists the running shells with their id, process id, shell name, program, launch time and log file. With `-j` it prints a JSON array, one object per running shell, with `id`, `pid`, `shellName`, `program`, `launchTime`, `pipe` and `log`. Stopped shells are not listed. While the feature shell runs, its object's `pid` is the process id the other steps use as `{pid}`.

## Sub-features

- `list-text` prints each running shell as `Instance <id>:` with its `Process ID`, `Shell name`, `Program`, `Launch time` and `Log file` under it.
- `list-json` prints the running shells as a JSON array with `-j` or `--json`.
- `list-empty` prints `No running instances. Start one with winshell run.`, or `[]` with `-j`, when no shell is running.
- `list-child` lists the shell's own process, not the `winshell run` that supervises it.

## How to get to it (user POV)

- Run `winshell list` in a terminal.
- Run `winshell list -j` or `winshell list --json` for JSON, such as to read a shell's process id in a script.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **List as text.** List the running shells. Run `winshell list`. It exits with 0, and under the feature shell's process id it prints its shell name, `features`, the folder it was copied into, and its program, `FeatureShell.exe` in `bin\shell`.
- **List as JSON.** List the running shells as JSON. Run `winshell list -j`. It exits with 0, and one object has the feature shell's process id as its `pid` (`"pid": {pid},`).

```json
[
  {"winshell": ["list"], "output": "^  Process ID: {pid}\\r?\\n  Shell name: features\\r?\\n  Program: .*\\\\features\\\\bin\\\\shell[^\\\\]*\\\\FeatureShell\\.exe\\r?$"},
  {"winshell": ["list", "-j"], "output": "\"pid\": {pid},"}
]
```

## Gotchas

- `list` reads the shells of one local folder, `%LOCALAPPDATA%\winshell` or `WINSHELL_LOCAL_DIR`. The session sets its own, so the user's shells are not listed, and the session's shells are not in the user's `winshell list`.
- `program` is the program that runs, not the `.csproj`: the build in `bin\shell` under the project folder, or the Debug build under `winshell dev`.
- The JSON leaves out `processStart` and `exitedAt`. A stopped shell is not listed, but `winshell log` can still print its log.
- In PowerShell, set `[Console]::OutputEncoding = [Text.UTF8Encoding]::new()` before you read the output, because a path outside ASCII is written as itself in UTF-8.
