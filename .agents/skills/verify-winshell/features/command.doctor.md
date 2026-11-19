# winshell doctor

`winshell doctor` checks this PC's setup for a shell and prints one line per check, with a fix under each warning and failure. It reads the project as data, never builds it, and changes nothing. With `-j` it prints one JSON object: the `version`, a `checks` array in which each check has `id`, `title`, `status`, `message` and `fix`, and a `summary` that counts `pass`, `warn` and `fail`. It exits with 1 when a check fails, and with 0 otherwise.

## Sub-features

- `doctor-checks` checks Windows, .NET, the folders Winshell writes, app bars, DPI, the sign-in entry, notification access, the running shells, recent crash reports, and the shell project.
- `doctor-project` checks that the project is there, that it has been built (`Built`), and that the build is up to date (`Build`), for the shell `-s` or `-p` names, or the one Winshell would run.
- `doctor-preferences` names the line of the shell's preferences file that is not valid TOML, even while the shell is not running. It finds the file by the `ShellName` MSBuild reads from the project with the settings `winshell run` builds with, or the folder name without one.
- `doctor-sign-in` says what is wrong with the sign-in entry, and names `winshell use <name>` in its fix when the entry starts another shell.
- `doctor-json` prints the checks as JSON with `-j` or `--json`.
- `doctor-color` prints without color with `--no-color`, with `NO_COLOR` set, or when the output is redirected.
- `doctor-exit` exits with 1 when any check fails. A warning alone exits with 0.

## How to get to it (user POV)

- Run `winshell doctor` in a terminal to check the shell `winshell run` would start.
- Run `winshell doctor -s <name>` or `winshell doctor -p <path>` to check another shell.
- Add `-j` or `--json` for JSON, or `--no-color` for plain text.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's project has been built to `bin\shell`, as `winshell run -d` does when the driver starts it.

- **Check the feature shell's project.** Ask for the checks of the feature shell's project as JSON. Run `winshell doctor -p {shell} -j`. It exits with 0 or 1, and the `checks` array has the check with `"id": "built"`, `"title": "Built"` and `"status": "pass"`.

```json
[
  {"winshell": ["doctor", "-p", "{shell}", "-j"], "exit": [0, 1], "output": "\"id\": \"built\",\\s*\"title\": \"Built\",\\s*\"status\": \"pass\""}
]
```

## Gotchas

- Most checks read this PC, not the session: notification access, the sign-in entry, app bars and DPI. One of them can fail on a given machine, so the step accepts exit 1 and checks only `Built`.
- `doctor` never builds. `Built` passes only because `winshell run -d` built `bin\shell` before the feature ran. A project that was never built fails `Built` with `<name> has not been built.`
- `Built` and `Build` are two checks: `Built` says a program is in `bin\shell`, and `Build` says whether a source is newer than it. Match the `id`, not only the title.
- The check that names `winshell use <name>` is the Project check. In the output it is the check with `"id": "project"` and the title `Project`.
