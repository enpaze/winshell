# winshell help

`winshell help` prints the usage of a command, such as `winshell help list`, and runs nothing. With no command it prints the summary: the version, one line for each command, the options and the exit codes. It needs no running shell and exits with 0.

## Sub-features

- `help-summary` prints the version, each command, the options and the exit codes for `winshell help`, `winshell --help` and `winshell` with no arguments.
- `help-command` prints one command's usage for `winshell help <command>`.
- `help-flag` prints the same usage for `winshell <command> --help`, `-h` or `/?`, and does not run the command.
- `help-unknown` prints `Unknown command "<name>". Run winshell --help.` and exits with 2 for a word that is not a command.
- `help-one-word` takes one command only: a second word, such as `winshell help inspect click`, prints `Unexpected argument "click".` and exits with 2.

## How to get to it (user POV)

- Run `winshell help [command]` in a terminal.
- Run `winshell <command> --help`, `winshell <command> -h` or `winshell <command> /?`.
- Run `winshell` or `winshell --help` for the summary.

## Driving it with winshell inspect

Preconditions:

- Shell: `none`.

- **Print list's usage.** Ask for the usage of `list`. Run `winshell help list`. It exits with 0, and its output starts with `winshell list`, the usage line `winshell list [-j]`.

```json
[
  {"winshell": ["help", "list"], "output": "^winshell list"}
]
```

## Gotchas

- `winshell` with only the options of `run`, such as `winshell -d`, starts a shell instead of printing help. So do `winshellw` and a start with no console window.
- The subcommands of `ipc` and `inspect` have no usage of their own. `winshell help inspect` lists every `inspect` command.
- The summary names the shells folder, `%APPDATA%\winshell` or `WINSHELL_SHELLS_DIR`, so its text differs from session to session.
- `-V` prints the version with any command but `help`.
