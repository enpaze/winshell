# A shell project lives anywhere, and the chosen shell is a copy

A shell project lives wherever its author keeps code, and Winshell's commands act on the project in the current folder, as `dotnet` commands do. `winshell new MyShell` creates `.\MyShell`. `winshell dev` runs the project in this folder and applies each save. `winshell use` builds it and copies the program into Winshell's own folder: that copy is the chosen shell, which `winshell start` and sign-in run. Winshell remembers nothing else about where shells are.

Projects used to live in a config folder, `%APPDATA%\winshell\<name>`, and `winshell run` found one through five steps: the chosen name, the folder's own project, `default`, the only subfolder, else nothing. That is how a Linux shell toolkit keeps its configs. A C# developer expects a project under their own source control, and could not guess the search. Sign-in also ran `bin\shell` inside the project, so a `git clean`, a branch switch or a deleted folder broke the next sign-in.

This amends [one preferences file per shell](0002-one-preferences-file-per-shell.md): the file is `%APPDATA%\winshell\<shell name>.toml`, and the shell name is always the project file's name. The rest of that decision stands.

## Considered options

- **Keep the config folder and shorten the search.** Still a hidden folder to learn, and still a name to pass to every command.
- **Remember the project's path and run its `bin\shell`.** No copy to make, but the shell a user signs in to would change whenever its author built, cleaned or moved the project.
- **Build at sign-in.** Always current, but signing in would need the .NET SDK and would run a half-finished edit.

## Consequences

- Edits and dev sessions never change the chosen shell. `winshell use` is the one way to update it, so there is no `reload` command and no `Shell.Reload()`.
- A `winshell use` that does not build leaves the chosen shell as it was.
- `winshell start` and `winshell stop` are remembered: after `stop`, nothing starts at sign-in until `start`. No other switch turns sign-in on or off, and a shell cannot change it for itself.
- The dev run and the chosen shell of one project share a shell name, so they share its preferences file and its data.
- Sharing a shell means sharing its project. Whoever receives it needs the .NET SDK to run `winshell use`. The built copy is what a later way to share a shell without the SDK would hand over.
