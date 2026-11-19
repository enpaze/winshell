# winshell inspect record

`winshell inspect record` records a window of a running shell as an MP4 for a given time. It waits for the end, then prints the size, length, rate and frame counts, such as `<width>x<height>, 2.0 s at 60 fps: ...`. Windows delivers a frame each time the window changes, and the MP4 has a constant rate: the refresh rate of the window's screen, or `--fps <n>`. `winshell` captures and encodes in its own process, so the shell it records is not slowed, and nothing goes through Windows' input. On the `features` shell, the driver records the `Winshell features` window, whose content type is `FeatureWindow`.

## Sub-features

- `record-window` records the window that `[window]` names, by its content type or its number in `tree`, or the first window without one.
- `record-screen` records screen `n` with `--screen [n]`, counted from 0 with the primary screen first, instead of a window.
- `record-fps` keeps at most `n` frames a second with `--fps <n>`, and makes the MP4 `n` frames a second.
- `record-frames` also saves each kept frame as a PNG named by its milliseconds, and `contact-sheet.png` of them all, with `--frames <folder>`.
- `record-rate-fallback` uses 120, 60 or 30 frames a second when the encoder refuses the screen's rate for the frames' size, and says which.
- `record-on-screen` records the window's place on its screen, with anything over it, when Windows refuses to capture the window by itself, and says so.
- `record-refuse` exits with 2 without `--for`, with a file that doesn't end in `.mp4`, or with both `--screen` and a window, and with 1 into a `--frames` folder that has files in it.

## How to get to it (user POV)

- Run `winshell inspect record <file.mp4> --for <time> [window]` in a terminal. A time is in seconds, such as `5` or `2.5s`, or in milliseconds, such as `500ms`.
- Add `--screen [n]`, `--fps <n>` or `--frames <folder>` as needed.
- Pick the shell with `--pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>`. With none, the command acts on the only running shell.
- Start it in the background to run other `inspect` commands, such as `point`, while it records.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.
- The feature shell's `Winshell features` window is open.

- **Record the feature window.** Record the window that shows `FeatureWindow` for two seconds. Run `winshell inspect --pid {pid} record {scratch}/window.mp4 --for 2s FeatureWindow`. It exits with 0 after about two seconds, and `{scratch}/window.mp4` exists and is not empty.

```json
[
  {"winshell": ["inspect", "--pid", "{pid}", "record", "{scratch}/window.mp4", "--for", "2s", "FeatureWindow"], "file": "{scratch}/window.mp4"}
]
```

## Gotchas

- The step checks only that the MP4 exists and is not empty. To check what it shows, add `--frames <folder>` by hand and look at `contact-sheet.png`.
- A window that does not change gives few frames. Each frame repeats in the MP4 until the next one.
- Some versions of Windows refuse to capture a tool window by itself, which every Winshell panel is. Windows Server 2025 does, and Windows 11 build 26200 doesn't. Then the recording shows the window's place on its screen with anything over it.
- The pointer is in the picture. A popup in a window of its own, such as a menu, needs `--screen`.
- The command blocks for the whole `--for` time. Use a short time on the desktop you share.
