# winshell stop

`winshell stop` asks one running shell to exit, waits until its process has exited, and prints `Stopped <id>`. A shell that exits this way puts Explorer's taskbar back if it hid it, and `winshell run` does not start it again. After the feature shell is stopped, `winshell list -j` prints `[]` because no shell is running.

## Sub-features

- `stop-quit` asks the shell to quit, waits until the process has exited, and prints `Stopped <id>`.
- `stop-force` with `--force`, ends the process when it does not exit.
- `stop-refuse` without `--force`, leaves a shell that does not exit running, prints `Instance <id> is still running. Use --force to end it.` and exits with 1.
- `stop-pick` stops the shell that `-i`, `--pid`, `-s` or `-p` names, or the only running shell. With several and no selector, it exits with 2 and lists them. With none, it prints `No running instances.` and exits with 1.
- `stop-no-restart` stops supervision: `winshell run` does not start a shell again that `winshell stop` asked to stop, even one that `--force` ended.
- `stop-any-release` works when the CLI and the shell come from different releases, which every other command refuses.

## How to get to it (user POV)

- Run `winshell stop` in a terminal to stop the only running shell.
- Run `winshell stop --pid <pid>`, `winshell stop -i <id>`, `winshell stop -s <name>` or `winshell stop -p <path>` to stop one of several.
- Run `winshell stop --force` to end a shell that does not exit when asked.
- A command can also come after the options, so `winshell -s work stop` stops that shell.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- No other shell of this session runs, such as a `winshell dev` shell from an earlier feature.

- **Stop the shell.** Stop the feature shell by its process id. Run `winshell stop --pid {pid}`. It exits with 0 and prints `Stopped ` and the instance id.
- **See that nothing runs.** List the running shells as JSON. Run `winshell list -j`. It exits with 0 and prints `[]`, an empty array, on a line of its own.

```json
[
  {"winshell": ["stop", "--pid", "{pid}"], "output": "^Stopped "},
  {"winshell": ["list", "-j"], "output": "^\\[\\]$"}
]
```

## Gotchas

- `stop` ends the feature shell, and the next step that uses `{pid}` starts it again. That new process has new state, such as a click count of 0.
- Always name the shell with `--pid`. The session's own `WINSHELL_LOCAL_DIR` keeps the user's shells out of the session's `winshell list`, but a `winshell` started without it sees the user's shell and can stop it.
- `stop --force` ends the process, so the shell does not put Explorer's taskbar back itself. `winshell run` does that when it sees the shell end. A shell that ran without `winshell run` leaves the taskbar hidden.
- `stop` waits until the process has exited, so `winshell list` does not list it afterwards. A stopped shell's log stays, and `winshell log` can still print it.
