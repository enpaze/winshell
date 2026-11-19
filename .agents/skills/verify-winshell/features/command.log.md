# winshell log

`winshell log` prints a shell's log file. Each line starts with the date and the time to the millisecond, then the level, the category and the message. The first line a shell writes when it starts names the Winshell version, the Windows version and the program, such as `2026-11-03 09:09:28.411  INFO shell: Winshell 0.7.0+… on Windows 10.0.26300.0, program "…"`. The log of a stopped shell stays for a day, so `winshell log` can print it after the shell has exited.

## Sub-features

- `log-print` prints the whole log of the running shell.
- `log-tail` prints only the last `<n>` lines with `-t <n>` or `--tail <n>`.
- `log-follow` keeps printing new lines with `-f` or `--follow`, until Ctrl+C.
- `log-stopped` prints the log of a stopped shell that `-i`, `--pid`, `-p` or `-s` names. With no selector and no shell running, it prints the log of the newest stopped one and writes `Showing the log of stopped instance <id>.` on stderr.
- `log-file` prints the file `winshell log <file>` names.
- `log-refuse` exits with 1 when a selector matches nothing, or with `No log at <path>.` when the file is not there.

## How to get to it (user POV)

- Run `winshell log` in a terminal for the running shell, or for the last stopped one.
- Run `winshell log -t <n>` for the last lines, or `winshell log -f` to keep printing new ones.
- Run `winshell log --pid <pid>`, `-i <id>`, `-s <name>` or `-p <path>` to pick one shell.
- Run `winshell log <file>` to print a log file, and `winshell log -- <file>` for a file whose name starts with `-`.
- Run the shell with `winshell run -v` to add `DEBUG` lines. A shell writes its own lines through `shell.Log`, such as `shell.Log.Warn("shell", "Could not restart.")`.

## Driving it with winshell inspect

Preconditions:

- Shell: `features`.

- **Read the last lines.** Print the end of the feature shell's log. Run `winshell log --pid {pid} -t 20`. It exits with 0, and one of the 20 lines is the shell's first line: a date, a time to the millisecond, `INFO shell: Winshell ` and the version.

```json
[
  {"winshell": ["log", "--pid", "{pid}", "-t", "20"], "output": "^[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}\\.[0-9]{3} +INFO shell: Winshell "}
]
```

## Gotchas

- `-t 20` keeps the last 20 lines only. The start line is among them only while the shell has written 19 lines or fewer after it, so a shell that has logged for a while fails this step.
- `INFO` and `WARN` have a leading space, so two spaces follow the time on those lines. The pattern allows one or more.
- The log file is kept to about 4 MB. The previous part is `log.1.txt` beside it, which `winshell log` does not print unless you name the file.
- A later start deletes a stopped shell's folder, log included, once it is a day old.
- The log names file paths and the apps the user runs. Look through it before you attach it to a report.
