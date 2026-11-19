# winshell update

`winshell update` installs the newest build of the saved channel. `stable` and `dev` are the channels. With no `--channel`, it uses `update.toml` in the shells folder, and stable when that file is missing.

## Sub-features

- `update-help` prints the usage and does not install.

## How to get to it (user POV)

- Run `winshell update` in a terminal.
- Run `winshell update --channel stable` or `winshell update --channel dev`.
- Run `winshell update -h` for the usage.

## Driving it with winshell inspect

Preconditions:

- Shell: `none`.

- **Print the usage.** Ask for help. Run `winshell update -h`. It exits with 0, and its output starts with `winshell update`.

```json
[
  {"winshell": ["update", "-h"], "output": "^winshell update"}
]
```

## Gotchas

- `winshell update` without `-h` installs into `%LOCALAPPDATA%\Programs\winshell` when this `winshell.exe` is the one in that folder. The drive uses `-h`, which does not install.
