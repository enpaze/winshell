# winshell reload

`winshell reload` builds a running shell's shell project and replaces the shell with a new process from that build. Under `winshell run`, it prints `Reloaded <id>` once the old process has exited, and `winshell run` starts the new one at once. When the build fails, the running shell stays, a notice shows the compiler's lines, and the command exits with 1. Every window is replaced, so on the `features` shell the new process's `features.clicks` reads `clicks: 0`.

## Sub-features

- `reload-build` builds the project before it replaces the shell.
- `reload-supervised` under `winshell run`, asks the shell to quit, prints `Reloaded <id>` once the old process has exited, and lets `winshell run` start the last build and count its crashes from none.
- `reload-unsupervised` for a shell with no supervisor, has the shell start the new process, and prints `Reloaded <id>` once that start is accepted, before the old process quits.
- `reload-build-error` leaves the running shell up, shows a notice with the compiler's lines, prints why, and exits with 1.
- `reload-refused` leaves the process running and exits with 1 when the quit was refused or never reached the shell, or when the project of a shell under `winshell run` can't be found.
- `restart-self` lets a shell restart itself with `shell.Restart()`, which starts its program again without a build and quits. That new process runs with no supervisor.

## How to get to it (user POV)

- Run `winshell reload` in a terminal for the only running shell.
- Run `winshell reload --pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>` to pick one of several.
- A shell calls `shell.Restart()`, which returns `false` when the new process did not start.
- On a build error, choose **Dismiss** in the notice, or run `winshell ipc call notice dismiss`, to close it.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell runs under `winshell run`, as `winshell run -d` starts it.

- **Reload.** Rebuild and replace the feature shell. Run `winshell reload --pid {pid}`. It exits with 0 and prints `Reloaded ` and the instance id.
- **Read the count on the new shell.** Read the tree of the new process. Run `winshell inspect --pid {pid} tree`. Exactly one element is named `features.clicks`, and its `text` reads `clicks: 0`. The driver retries for 60 seconds while the new process starts, and takes `{pid}` from the new process.

```json
[
  {"winshell": ["reload", "--pid", "{pid}"], "output": "^Reloaded "},
  {"element": "features.clicks", "has": {"text": "^clicks: 0$"}, "within": 60}
]
```

## Gotchas

- The recipe does not click before the reload, so `clicks: 0` also holds on a shell that was never replaced. To see the count reset, run `winshell inspect --pid {pid} click features.click` before the reload and check that the count is 1 or more.
- The process id changes. A `--pid` from before the reload names a shell that has exited.
- State does not survive a reload unless the shell writes it itself.
- There is no file watcher. A saved edit reaches the shell only through `winshell reload` or `winshell dev`.
- Under `winshell run`, the command waits up to five seconds for the shell to answer and five more for it to exit.
