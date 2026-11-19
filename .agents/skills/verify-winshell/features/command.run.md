# winshell run

`winshell run` builds a shell project to `bin\shell` when a source is newer than its program, then starts that program and supervises it until it exits. A shell that crashes starts again. With `-d` it builds, starts the shell in the background with no console window, and returns at once. Starting a shell asks any shell already running in this local folder to quit first. After the feature shell is stopped and started again with `-d`, `winshell list -j` lists `FeatureShell.exe` again.

## Sub-features

- `run-build` builds to `bin\shell` when a source is newer, then starts the program. A failed build prints why and does not start the last program that built.
- `run-pick` runs the project that `-p <path>` or `-s <name>` names, or else the one `winshell use` chose, the project in the shells folder, its `default` folder, or its one subfolder with a project. With none, it starts nothing, prints `No shell project in <folder>, so nothing started. Run winshell new to make one.` and exits with 1. A start with no console window, such as `winshellw`, the Start tile or the sign-in entry, prints nothing and exits with 0.
- `run-daemonize` with `-d` or `--daemonize`, starts the shell in the background with no console window and returns at once.
- `run-no-build` with `--no-build`, runs the program the last build left in `bin\shell`.
- `run-no-duplicate` with `-n` or `--no-duplicate`, exits 0 without a message when this shell name is already running, and does not replace it.
- `run-replace` asks a shell already running in this local folder to quit, waits until it has exited, then starts. One that does not quit stays, and the start exits with 1.
- `run-supervise` starts a crashed shell again after 1, 2, 4, 8 and then 10 seconds, and ends supervision after the sixth crash in a row. It shows Explorer's taskbar again after every exit but 0.
- `run-errors` prints a failure before the log exists to the console, and shows it in a dialog when there is no console, with **Open shell folder**.

## How to get to it (user POV)

- Run `winshell run` in a terminal, with `-p <path>` or `-s <name>` to name the project, and `-d`, `--no-build`, `-n` or `-v` as needed. Press Ctrl+C to stop the shell.
- `winshell` with only the options of `run`, and `winshellw`, start a shell too.
- Choose Winshell's Start tile, or sign in with the sign-in entry that `install.ps1 -AutoStart` adds. Both start what `winshell run` would, and the sign-in entry passes `--no-build`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Stop the running shell.** Stop the feature shell. Run `winshell stop --pid {pid}`. It exits with 0 and prints `Stopped ` and the instance id.
- **Start it in the background.** Start the feature shell's project again. Run `winshell run -d -p {shell}`. It exits with 0 once the build is up to date and the shell has been started in the background.
- **See it run.** List the running shells as JSON. Run `winshell list -j`. It exits with 0, and a `program` names `FeatureShell.exe`. The driver retries for 30 seconds while the shell starts.

```json
[
  {"winshell": ["stop", "--pid", "{pid}"], "output": "^Stopped "},
  {"winshell": ["run", "-d", "-p", "{shell}"]},
  {"winshell": ["list", "-j"], "output": "FeatureShell\\.exe", "within": 30}
]
```

## Gotchas

- Without `-d`, `winshell run` stays in the terminal until supervision ends. The driver always uses `-d`.
- `run -d` exits with 0 once the start is handed off, so its exit code proves the build, not the start. The `winshell list -j` step is the proof that the shell runs.
- A start with no console window and no project exits with 0 and prints nothing, so an exit code of 0 alone proves nothing.
- A new start replaces a shell that runs in the same local folder. Do not start a second shell in a session to compare two.
- The first build of a project can take minutes. Here the project was built when the driver first started it, so the build is up to date.
