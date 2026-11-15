# One shell runs per Windows session

Winshell runs one shell at a time in a Windows session, and starting another replaces the one that is running. Any number could run together before, so every command that reaches a shell had to say which one: `-i`, `--pid`, `-c` and `-p` on `kill`, `reload`, `log`, `ipc` and `inspect`, a `list` command to find the ids, `--no-duplicate` to avoid a second copy, and an error for "several instances, pick one". An author met all of that the first time they tried a second project while their own bar was up. With one shell, a command needs no target and "instance" leaves the author's vocabulary.

## Considered options

- **Several shells, as before.** One program for the bar and another for the launcher keeps a crash in one from taking the other down. It costs a selector on every command, and two shells that both reserve an edge or hide Explorer's taskbar have to be told apart by the author.
- **Several shells, with a default one.** Commands would need no target most of the time, and the selectors, `list` and the ambiguity error would all still exist for the rest.

## Consequences

- An author who wants a bar and a launcher puts both in one shell. A shell can open any number of windows, so nothing is lost but the separate process.
- `winshell dev` in a project replaces the chosen shell while it runs, and starts it again when the dev session ends.
- "One" is counted per Winshell data folder. Tests that run probe shells side by side keep their isolation by giving each its own folder, as they do today.
