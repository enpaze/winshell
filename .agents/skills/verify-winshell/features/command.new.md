# winshell new

`winshell new` with no name lists the starter and each example this release carries, one a line: `starter` first, as `A bar with a clock and a battery`, then each example with the title of its README. With a name, it copies that one into a new folder, prints `Created <folder>`, and prints the command that runs the copy, such as `Run it: winshell run -c starter`. The copy's project references the `Winshell` package of this release, and no file in the Winshell repo.

## Sub-features

- `new-list` lists the starter and each example with a short description.
- `new-copy` copies the starter or an example into `<name>` under the config folder, `%APPDATA%\winshell` or `WINSHELL_CONFIG_DIR`.
- `new-path` copies into the folder that `-p <folder>` or `--path <folder>` names instead.
- `new-package` adds a `nuget.config` with the `packages` folder beside `winshell.exe` as a source, when `Winshell.<version>.nupkg` is in it. Otherwise it prints that the package is not beside `winshell.exe` and where to read how to add a source.
- `new-refuse` prints `No starter or example is named "<name>". Run winshell new to list them.` for an unknown name, and `<folder> already exists and isn't empty. Pick another folder with -p.` for a folder in use. Both exit with 2 and change nothing.
- `new-shell-id` gives each copy a shell id from its folder, so two copies keep their data apart.

## How to get to it (user POV)

- Run `winshell new` in a terminal to list what it can copy.
- Run `winshell new <name>`, such as `winshell new starter` or `winshell new omarchy-style`, to copy one into the config folder.
- Run `winshell new <name> -p <folder>` to copy it somewhere else.
- Then run the copy with `winshell run -c <name>`, or develop it with `winshell dev -c <name>`.

## Driving it with winshell inspect

Preconditions:

- Shell: `none`.
- The feature's evidence folder has no `starter` folder in it yet.

- **List what it can copy.** List the starter and the examples. Run `winshell new`. It exits with 0, and a line starts with two spaces and `starter `.
- **Copy the starter.** Copy the starter into the feature's evidence folder. Run `winshell new starter -p {scratch}/starter`. It exits with 0, a line starts with `Created `, and `{scratch}/starter/MyShell.csproj` exists and is not empty.

```json
[
  {"winshell": ["new"], "output": "^  starter "},
  {"winshell": ["new", "starter", "-p", "{scratch}/starter"], "output": "^Created ", "file": "{scratch}/starter/MyShell.csproj"}
]
```

## Gotchas

- A `winshell` built from a clone has no `packages` folder beside it. The copy then gets no `nuget.config`, the command says so, and the copy does not build until you add a source for the `Winshell` package, as [From a clone](../../../../docs/configuration.md#from-a-clone) shows. The steps check the files only, not a build.
- Without `-p`, the copy goes into the config folder. The session sets `WINSHELL_CONFIG_DIR`, so a copy goes into the session's config folder, not `%APPDATA%\winshell`.
- A second copy into the same folder is refused with exit 2, so run the recipe with a fresh evidence folder.
- The starter's project file is `MyShell.csproj` whatever the folder is called.
- With `-p`, the last line names the folder, as `Run it: winshell run -p <folder>`, not `-c <name>`.
