# winshell dev

`winshell dev` builds a shell project as a Debug build, starts it, and keeps it running under `dotnet watch` until you press Ctrl+C. Each C# edit you save reaches the running shell through Reactor's hot reload in about a second, without a restart. Code that does not compile shows in a notice on screen, and the last version that compiled keeps running. While it runs, `winshell list` shows the shell's program in the project's Debug build folder.

## Sub-features

- `dev-start` builds the project as a Debug build and starts it.
- `dev-hot-reload` applies each saved C# edit to the running shell without a restart. A widget that shows an edited type is made again with its widget area, and the other windows stay as they are.
- `dev-restart` restarts the shell for an edit .NET can't apply to running code, such as a new base class.
- `dev-build-error` shows code that doesn't compile, and a TOML file with an error, in a notice, and keeps the last version that compiled running. The notice goes when the next save compiles.
- `dev-stop` closes the shell and stops `dotnet watch` on Ctrl+C, or when the terminal closes.
- `dev-no-project` prints `winshell dev needs a shell project. Run winshell new to make one.` and exits with 1 when there is no project.

## How to get to it (user POV)

- Run `winshell dev` in a terminal for the shell `winshell run` would start.
- Run `winshell dev -s <name>` for the project in `<name>` under the shells folder, such as `winshell dev -s starter`.
- Run `winshell dev -p <path>` for a `.csproj`, or for the folder that has one, such as `winshell dev --path .`.
- Press Ctrl+C in that terminal, or close it, to stop the shell and `dotnet watch`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The .NET SDK that `global.json` names is installed, because `dotnet watch` builds the project.

- **Start dev.** Start the feature shell's project under `dotnet watch`. Run `winshell dev -p {shell}`. It keeps running in the background until the feature ends, and the driver does not check its output.
- **See the Debug build run.** List the running shells. Run `winshell list -j`. It exits with 0, and a `configPath` names `FeatureShell.exe` in a folder whose path has `Debug` in it (`Debug[^"]*FeatureShell\.exe`). The driver retries for up to 600 seconds while the first Debug build runs.

```json
[
  {"winshell": ["dev", "-p", "{shell}"], "background": true},
  {"winshell": ["list", "-j"], "output": "Debug[^\"]*FeatureShell\\.exe", "within": 600}
]
```

## Gotchas

- The first `dotnet watch` build is a full Debug build, separate from the build in `bin\shell`. It can take minutes, so the step retries for 600 seconds.
- `winshell dev` starts a shell, and starting a shell asks the shell already running in this local folder to quit. The `features` shell that `winshell run -d` started gives way to the Debug build. The next feature that needs `{pid}` starts the `features` shell again.
- The driver ends `winshell dev` when the feature ends. Ctrl+C has the same effect: it closes the shell and stops `dotnet watch`.
- The steps prove only that the Debug build starts and runs. They do not save an edit, so hot reload, a restart and the build-error notice are not verified by this file.
- Desktop apps opened from the shell stay open when `winshell dev` starts the shell again, and when `winshell dev` ends.
