# Command line

`winshell` talks to the instances that are running, checks this PC's setup and copies a configuration to start from. `winshell run` starts a shell. `winshell` with no arguments prints this summary, and so does `winshell --help`. `winshell <command> --help`, `-h` or `/?` prints that command's usage and does not run the command, and so does `winshell help <command>`. A word that is not a command prints `Unknown command "<name>". Run winshell --help.`, whatever follows it, and a first word after `ipc`, `inspect` or `widget` that is not one of theirs prints `Unknown ipc command "<name>". Run winshell ipc --help.`, or the same for `inspect` and `widget`. A command without the words it needs says what is missing, such as `winshell inspect screenshot needs a file.`, and `ipc`, `inspect` or `widget` on its own prints `winshell ipc needs a command. Run winshell ipc --help.` Each of these exits with code 2. So does an option the command does not take: `winshell list --force` prints `winshell list does not take "--force".` Every command takes `-v`. With any command but `help`, `-h` prints that command's help and `-V` the version, and it exits. `winshellw`, and `winshell` with only the options under [Run a shell](#run-a-shell), still start a shell. That is how the Start tile and the sign-in entry launch it.

```text
winshell run [options]              start a shell
winshell                            print this help
winshell <command> --help           help for that command, which does not run
winshell list [-j]                  list running instances
winshell kill [--force] [instance options]
                                    stop an instance and wait until it exits
winshell reload [instance options]
                                    reload an instance's configuration
winshell dev [-c <name> | -p <path>]
                                    run a project and apply each edit to it
winshell log [-f] [-t N] [file]     print an instance's log
winshell ipc show [-j]              list IPC targets
winshell ipc call <target> <function> [args...]
winshell ipc prop get <target> <property>
winshell ipc wait <target> <signal>
winshell ipc listen <target> <signal>
winshell inspect tree | screenshot <file> | record <file.mp4> --for <time>
                 | click <name> | hover <name> | focus <name>
                 | point <name> | leave | press <name> | scroll <name> <steps>
                 | key <keys>
                                    look at a running shell, or act on it
winshell doctor [config options] [-j] [--no-color]
                                    check this PC's setup for Winshell
winshell new [<name>] [-p <folder>] list the examples, or copy one to start from
winshell use [<name>]               choose the config sign-in starts, and switch to it
winshell widget install <source>[@ref] [--yes]
                                    install widgets from a GitHub repository
winshell widget list                list the widgets a configuration has
winshell widget remove <name>       remove an installed widget
winshell widget update [<name>] [--yes]
                                    move installed widgets to the ref's new commit
winshell help [command]             print this summary, or that command's help
```

## Run a shell

`winshell run` with no project starts nothing: it exits with 0 and prints nothing, and so do the Start tile and the sign-in entry. A `--path` or `--config` that names no project is still an error. With a project it builds to `bin\shell` when a source is newer than that program, then starts the program. A failed build does not start the last program that built. Options may come before `run`. [Where a configuration lives](../configuration.md#where-a-configuration-lives) explains which project it picks. A command may also come after its options, so `winshell -c work kill` stops that config instead of starting it.

| Option | What it does |
| --- | --- |
| `-p`, `--path <path>` | Builds and runs this `.csproj`, or the one `.csproj` in this folder. |
| `-c`, `--config <name>` | Builds and runs the project in `<name>` under the config folder, `%APPDATA%\winshell` or `WINSHELL_CONFIG_DIR`. The name is one folder: use `-p` for a path. |
| `--no-build` | Runs the program the last build left in `bin\shell`. Does not build, so a newer source is left unbuilt. The sign-in entry uses this. |
| `-d`, `--daemonize` | Builds the project, if there is one, then starts the shell in the background, with no console window, and returns at once. |
| `-n`, `--no-duplicate` | Exits 0 without starting, and without a message, when this shell id is already running. Two starts that overlap do the same: only one of them keeps running. |
| `-v`, `--verbose` | Also logs debug messages. |
| `-V`, `--version` | Prints the version, such as `winshell 0.6.0+e8d05d1…` for a build from source, and exits. |
| `-h`, `--help` | Prints `run`'s usage, or with no command the summary, and exits. |

The program a project builds also takes `--no-duplicate` and `-d` on its own command line, and then runs with no supervisor. `--no-duplicate` exits at once when its shell id is already running. `-d` starts that program again and returns. A start that carries `--after` keeps it.

The log goes to the console and to the instance's log file. The file is kept to about 4 MB, and the previous part is `log.1.txt` beside it. Ctrl+C in the console stops the shell.

### Crashes

`winshell run` runs the shell in a child process and stays until supervision ends; `winshell list` shows that child. `winshell run` exits with the code the shell's last process exited with. When the shell exits:

- **0** is a stop that was asked for: `winshell kill`, a reload that handed over to a new process, or a `--no-duplicate` start that found its shell running. The shell has put Explorer's taskbar back itself.
- **1** is a shell that could not start and said why, which a new start would repeat. Task Manager's **End task** also ends a process with 1. Nothing starts again.
- **Any other code** is a crash. The shell starts again after 1, 2, 4, 8 and then 10 seconds; a configuration's program starts from its last build. The sixth crash in a row ends supervision. A shell that ran for a minute before it crashed starts the count again. A shell that `winshell kill` had asked to stop, such as one `kill --force` ended, does not start again.

After every exit but 0, `winshell run` shows Explorer's taskbar again and reserves its edge, unless a taskbar window was already hidden when that shell started: that taskbar belongs to another shell. So a crash that skipped the shell's own exit does not leave the taskbar hidden. A reload is not a stop. `winshell reload` of a configuration's program asks it to quit, and ends it five seconds later if it has not. `winshell run` then starts the last build at once, whatever the exit code, and counts that shell's crashes from none. A configuration's program that reloads itself with `shell.Reload()` hands over to a new process of itself, which runs with no supervisor. Ctrl+C, closing the console and signing out end supervision too: they ask the shell to quit, end it two seconds later if it has not, and start nothing again.

A failure before that log exists, such as a `--path` with no project, is printed to the console. The Start tile, the sign-in entry and any other start with no console window also show that message in a dialog, unless another program is reading the error. **Open config folder** opens the `--path` folder, or the config folder when there was no `--path`. An attached console prints the message and shows no dialog.

A build that fails prints why and does not start a program. `Building a configuration needs the .NET SDK, from https://dot.net/download.` is the message when the SDK is missing. A downloaded `.csproj`, or a `.props` or `.targets` beside it, is refused before the build: the file name, then `was downloaded from the internet. It runs as you.`

Any other option is an error, and exits with code 2. An option winshell does not have prints `Unknown option "<name>".`, and one of another command prints `winshell run does not take "<name>".`

## Pick an instance

`kill`, `reload`, `log`, `ipc` and `inspect` act on one running instance:

| Option | Picks |
| --- | --- |
| `-i`, `--id <id>` | The instance with this id, or the one whose id starts with it |
| `--pid <pid>` | The instance with this process id |
| `-p`, `--path`, `-c`, `--config` | The instance running that configuration |
| None | The only running instance. With several, the command exits with 2 and lists them. |

## Commands

| Command | What it does |
| --- | --- |
| `winshell list` | Lists the running instances with their id, process id, shell id, configuration, launch time and log file. `-j`, `--json` prints them as JSON. Stopped instances are not listed. The shape is under [JSON](#json). |
| `winshell kill` | Asks an instance to exit, waits until that process has exited, and prints `Killed <id>`. `--force` ends the process when it does not exit. `kill` is the one command a CLI and a shell of different releases still accept. |
| `winshell reload` | Builds the configuration. On failure the running instance stays, a notice shows, and the command prints why and exits with 1. On success a new instance takes over, then the command prints `Reloaded <id>`. Under `winshell run`, a configuration's program quits and `winshell run` starts the new one; otherwise the instance starts it with `--after`. A shell under `winshell run` whose project can't be found keeps running, and the command prints why and exits with 1. There is no file watcher. |
| `winshell dev` | Runs a configuration project under `dotnet watch`. Reactor's hot reload applies each edit to the running shell, until Ctrl+C. With no project it prints `winshell dev needs a configuration project. Run winshell new to make one.` and exits with 1. See [Develop a shell](../configuration.md#develop-a-shell). Takes `-c` and `-p`. |
| `winshell log` | Prints an instance's log. `-i`, `--pid`, `-p` and `-c` can also name a stopped instance. With no selector and no instance running, it prints the log of the newest stopped one and says so on stderr. A selector that matches nothing is an error. `-t`, `--tail <n>` prints the last `n` lines; `-f`, `--follow` keeps printing new lines until you press Ctrl+C. `winshell log <file>` prints that file. |
| `winshell ipc show` | Lists the instance's IPC targets with their functions, properties and signals. `-j`, `--json` prints that list as JSON. The shape is under [JSON](#json). |
| `winshell ipc call <target> <function> [args...]` | Calls a function registered with [`ShellIpc.Handle`](winshell.md#shellipc) and prints what it returns. |
| `winshell ipc prop get <target> <property>` | Prints a property registered with `ShellIpc.Property`. |
| `winshell ipc wait <target> <signal>` | Waits for a signal once and prints its first argument. |
| `winshell ipc listen <target> <signal>` | Prints the first argument of each signal until you press Ctrl+C. |
| `winshell inspect tree` | Prints each open window of an instance as JSON: its kind, place and size, and its live XAML tree, with each element's type, `x:Name`, automation id and name, position in the window, size and what it holds, such as `text`, `value`, `on`, `checked` or `selectedIndex`. What the window's open flyouts and menus show is under `popups`, each placed from its own corner. A password is left out. |
| `winshell inspect screenshot <file> [window]` | Saves a window as a PNG at its real size in pixels, margins included. The system draws a backdrop such as Mica, so a capture sits on the plain dark or light color of the theme instead. A `[window]` that names the type of an open popup's content, such as `MenuFlyoutPresenter`, saves that popup. |
| `winshell inspect record <file.mp4> --for <time> [window]` | Records a window as an MP4 for `<time>`: seconds, such as `5` or `2.5s`, or milliseconds, such as `500ms`. Windows delivers a frame each time the window changes, and each frame keeps the time it was drawn. The MP4 has a constant rate: the refresh rate of the window's screen, or `--fps <n>`. A frame repeats until the next one, and of several in one video frame only the last is shown. When the encoder refuses that rate for the frames' size, as H.264 does above about 120 frames a second for a whole 2560×1600 screen, the MP4 has 120, 60 or 30 instead, and the command says which. The size is the first frame's, rounded up to even numbers with black. `--screen [n]` records screen `n`, counted from 0 with the primary screen first, instead of a window. `--fps <n>` keeps at most `n` frames a second, the first in each `1/n` of a second. `--frames <folder>` also saves each kept frame as a PNG named by its milliseconds since the start, such as `000206.png`, and `contact-sheet.png`: every frame in a grid, read like text, each labeled with its number and milliseconds. The folder must be new or empty. Two frames in one millisecond are `000206.png` and `000206_2.png`. The sheet has every kept frame, so thousands of them make small thumbnails: `--fps` keeps a long recording readable. The pointer is in the picture. A popup in a window of its own, such as a menu, is recorded with `--screen`. Some versions of Windows refuse to capture a tool window by itself, which every Winshell panel is: Windows Server 2025 does, and Windows 11 build 26200 doesn't. Then the window's place on its screen is recorded, from where it was at the start, with anything over it, and the command says so. The shell names the window or screen, and `winshell` captures and encodes it in its own process, so the shell it records is not slowed. The command waits for the end, then prints the size, length, rate and frame counts. Run it in the background to use other `inspect` commands while it records. Nothing goes through Windows' input. |
| `winshell inspect click <name> [window]` | Activates an element through its UI Automation peer, as Narrator does: it invokes a button, toggles a toggle and opens what expands. A disabled control refuses. Nothing goes through Windows' input. |
| `winshell inspect hover <name> [window]` | Puts a control in its `PointerOver` look. A `PointerEntered` handler does not run, because XAML has no way to raise one from code. `point` does. |
| `winshell inspect focus <name> [window]` | Presses Tab in the element's window until keyboard focus is on the element or in it, so its focus rectangle shows, and prints how many Tabs it took. It refuses when Tab goes all the way round without reaching it, as it does for a panel or text. Real input: see below. |
| `winshell inspect point <name> [window]` | Moves the mouse onto the middle of the element, so `PointerEntered` and the `PointerOver` look run, and leaves it there until `leave`, the next of these real input commands, 30 seconds, or a move of the mouse. A move of the mouse leaves the cursor where it is now. Real input: see below. |
| `winshell inspect leave` | Puts the cursor back where it was before `point`, by way of a spot in the same window off the element, so the element sees the pointer leave and `PointerExited` runs. When the element fills its window, it may keep its hover look until the mouse moves. With nothing pointed at, because there was no `point` or it has ended, it says so. |
| `winshell inspect press <name> [window] [--right]` | Presses and releases the left mouse button on the middle of the element, or the right one with `--right`, so the pressed look, `Click`, `RightTapped` and the rest run. A press can activate a window that may take the keyboard. The cursor goes back as for `leave`, and the command says so when it couldn't. Real input: see below. |
| `winshell inspect scroll <name> <steps> [window]` | Turns the mouse wheel over the element, one notch a step: `3` scrolls down and `-3` up, at most 100 either way. The mouse comes onto the element as for `press`, and the notches go to the element's window only, so an element in a menu or flyout in a window of its own doesn't get them. The cursor goes back as for `leave`. Real input: see below. |
| `winshell inspect key <keys> [window]` | Presses keys in the window: one chord, such as `Enter`, or several separated by commas, such as `"Tab, Tab, Space"`, named as a [hotkey](winshell-input.md) names them. They go to the element keyboard focus is on; `focus` puts it on one. A key alone goes to the window, in front or not. A chord with Ctrl, Alt or Shift goes through the real keyboard, which Windows gives to the window in front, so it is refused when the window, or a menu it opened, isn't in front. A chord that never reaches the window in front is refused: the Windows key, Ctrl+Esc, Ctrl+Shift+Esc, Alt+Tab, Alt+Esc, and any chord a program, this shell included, has registered as a hotkey. A program that watches keys with a keyboard hook, as AutoHotkey does, still sees the chord. A key alone goes to the window's own input site, so a menu or flyout open in a window of its own doesn't get it. Real input: see below. |
| `winshell doctor` | Checks this PC's setup and prints one line per check, with a fix under each warning and failure. It reads a project as data and never builds. `-c` and `-p` pick a configuration, as they do when running a shell; with neither, it checks the one Winshell would run. `-j`, `--json` prints the checks as JSON. `--no-color` turns color off, as does `NO_COLOR` or a redirected output. It exits with 1 when a check fails, and it changes nothing. |
| `winshell new [<name>]` | With no name, lists the starter and each example this release carries, with the title of the example's README. With a name, copies that one into `<name>` under the config folder, `%APPDATA%\winshell` or `WINSHELL_CONFIG_DIR`, then prints `Created <folder>` and `Run it: winshell run -c <name>`. `-p`, `--path <folder>` copies into that folder instead. The copy's project references the `Winshell` package of this release, and no file in the Winshell repo. When `packages\Winshell.<version>.nupkg` is beside `winshell.exe`, the copy gets a `nuget.config` that adds that folder as a source; otherwise the command says where to get the package. A name that is not one of them prints `No starter or example is named "<name>". Run winshell new to list them.`, and a folder that exists and isn't empty prints `<folder> already exists and isn't empty. Pick another folder with -p.` Both exit with 2 and change nothing. |
| `winshell use [<name>]` | Chooses the configuration in `<name>` under the config folder as the one `winshell run` starts with no `-p` or `-c`, and so the sign-in entry and the Start tile too. It builds that configuration first when it has no build or a source is newer than its program, and a build that fails, or a project that was downloaded, prints why, exits with 1 and changes nothing. Then it prints `Chose <name>.`, and when other shells are running, stops each, waits until it has exited, prints `Stopped <id>`, and starts the chosen one in the background with `--no-build`, then prints `Started <name>`. A shell of the chosen configuration that is already running stays. One that does not stop leaves the choice made and the chosen one not started, and the command exits with 1. With no name, it lists the configurations in the config folder, one a line, with `*` before the one `winshell run` would start, and before none when it would start none, as with several and none chosen. With none at all, it prints `No configs in <folder>. Run winshell new to make one.` A `<name>` that is not one folder name, such as `..\other`, prints `winshell use needs the name of a config folder, not "<name>".` and exits with 2. A recorded name whose folder has no project is an error for `winshell run`, which says to choose another. |
| `winshell widget install <source>[@ref]` | Installs the widgets in a GitHub repository, a `file://` URL or a folder. `@ref` is a branch, a tag or a commit; with none, the default branch is used. The ref is resolved to a commit, and that commit is cloned under the local root, `%LOCALAPPDATA%\winshell\widgets\<owner>\<repo>\<commit>` or `widgets\local` for a folder or `file://` URL (`WINSHELL_LOCAL_DIR` replaces that root). `--project <path>` names the project when more than one lists widgets. The command prints the repository, the commit and the widget names, warns that building runs the repository's code and MSBuild targets in a program that starts at sign-in, and asks. `--yes` skips the question. Declining prints `Nothing was installed.` and changes nothing. A widget that needs a newer `Winshell` than the configuration uses is refused before the build, and the message names both versions. A widget name already installed from another source is refused, and so is a source that is already installed. The configuration is built against a staged `widgets.props` in a scratch folder, and the file beside the project is replaced only when that build succeeds. A failed build leaves the previous file and the previous program, and prints the build errors. Needs `git` and the .NET SDK. Takes `-c` and `-p`. |
| `winshell widget list` | Prints each installed widget's name, source, ref and commit, from `widgets.props`. With none, prints `No widgets are installed.` Takes `-c` and `-p`. |
| `winshell widget remove <name>` | Removes one installed source and every widget it contributed. `<name>` is a widget name or the source. The last source leaves `widgets.props` as an empty `<Project />`, which is newer than the program, so the next run rebuilds. `list` reads that file as nothing installed. Prints `Removed <source>.` Takes `-c` and `-p`. |
| `winshell widget update [<name>]` | Fetches the recorded ref of every installed source, or of the source `<name>` selects, and resolves it to a commit. `<name>` is a widget name or the source. A ref that still names the recorded commit prints `<source> is up to date at <commit>.` Otherwise the command prints the old and new commit, the commits `git log --oneline` lists between them, and the widget names that would be added or dropped, warns that building runs the repository's code and MSBuild targets in a program that starts at sign-in, and asks. `--yes` skips the question. Declining prints `Nothing was updated.` and leaves that source as it is. The version check and the staged build are the ones `install` uses. A failed build leaves the previous commit, `widgets.props` and the program. Older commits stay in the cache. Needs `git` and the .NET SDK. Takes `-c` and `-p`. |

In `winshell inspect`, a `[window]` is the type of a window's content, such as `BarView`, which names the first window that shows it in `tree` order, or its `window` number in `tree`, which names any one. Without one, `screenshot`, `record` and `key` take the first window, and the commands that take a `<name>` search every window. A `<name>` is an `x:Name`, an automation id or an automation name, in a window or in its open popups. A name that matches more than one element acts on none: the error lists each match with its window number, content type, element type, name, id and label, so a window number narrows it down when two bars both have a `MenuButton`. A match inside another counts as the outer one, such as a button and the card in it that carry the same label.

`focus`, `point`, `leave`, `press`, `scroll` and `key` are real input: the mouse and keys Windows delivers, so the handlers and looks a person's input runs run too. They are for checking a running shell, not for scripting one. One runs at a time, and each but `leave` refuses, sends nothing more and exits with 1 while someone has used the mouse or keyboard in the last 3 seconds. Input the shell sent itself doesn't count. `focus`, `key`'s plain keys and `scroll`'s notches are posted to the window itself, so they reach no other. `point`, `press` and `scroll` move the user's cursor, and they also refuse:

- when another window is on top at the element's middle, checked before every mouse event: the input would reach that window. A flyout or menu the element's window opened counts as that window.
- when the element is clipped away, covered by another element or not hit-testable at its middle, or has no size.
- when the element doesn't see the mouse come, or a wheel notch, within 2 seconds. A window that has just opened can miss the first move into it, so the move is sent again until then.
- when the mouse moves while they work.

The cursor goes back afterwards, unless the user has moved it: then it stays where they put it. Each of these commands first ends a `point`, putting the cursor back.

`winshell ipc call notice dismiss` closes Winshell's own notice for a build error or a failure. That notice is not a target the configuration registered, so `ipc show` does not list it. It takes no arguments.

A CLI and a shell whose releases differ, in major.minor.patch, refuse each other. The message is `This shell is Winshell <shell> and this winshell is <client>. Use the winshell that matches, or build the shell with this one.` `kill` is exempt.

An IPC request longer than 256 KiB is refused, which no command line can reach. `winshell ipc listen` keeps the latest signals when the client falls behind, and the shell releases that subscription when the client disconnects, even if the signal never fires.

A `--` ends options. Words after it are arguments, so `winshell log -- -f` prints the file `-f`. `ipc call` does the same after the function name, with no `--`: `winshell ipc call notes add -v` passes `-v` to `add`.

Output sent to a pipe or a file is UTF-8 without a byte order mark. In a console window it follows the console's code page. To read it in PowerShell, set `[Console]::OutputEncoding = [Text.UTF8Encoding]::new()` first, or PowerShell decodes it with the console's code page.

## JSON

`-j` and `--json` on `list` and `ipc show` write JSON to stdout. Characters outside ASCII are written as themselves, the same way `winshell doctor --json` writes a path.

`winshell list --json` prints a JSON array, one object per running instance:

| Field | What it is |
| --- | --- |
| `id` | The instance id. |
| `pid` | The process id. |
| `shellId` | The shell id. |
| `configPath` | The program that is running. |
| `launchTime` | When it started, as a local time with its offset. |
| `pipe` | The name of its control pipe. |
| `log` | Its log file. |

The array does not include `processStart` or `exitedAt`. Those stay in `instance.json`, the file the instance writes for itself, so that file can change without changing this command.

`winshell ipc show -j` prints a JSON array, one object per target, in ordinal order of the target name. Members stay in the order they were registered. A function `ipc call` cannot reach is left out, and so is a property of a type other than `string`, `int`, `bool`, `real`, `double` or `color`.

| Field | What it is |
| --- | --- |
| `target` | The target name. |
| `functions` | The functions `ipc call` can reach. Each has `name`, `parameters` and `returns`. |
| `properties` | The properties `ipc prop get` can read. Each has `name` and `type`. |
| `signals` | The signals registered on the target. Each has `name` and `parameters`. |

A parameter has `name` and `type`. `type` is null when the signal's parameter was declared without one.

```json
[
  {
    "target": "notes",
    "functions": [
      {
        "name": "hello",
        "parameters": [ { "name": "name", "type": "string" } ],
        "returns": "string"
      }
    ],
    "properties": [
      { "name": "title", "type": "string" }
    ],
    "signals": [
      {
        "name": "opened",
        "parameters": [ { "name": "query", "type": "string" } ]
      }
    ]
  }
]
```

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Done. For a shell, it was stopped. |
| 1 | No such configuration or instance, a build failed, an IPC call failed, the instance couldn't be reached or was still running, a `winshell doctor` check failed, a recording could not be made, such as into a `--frames` folder that has files in it, or real input was refused, such as while the PC is in use. Also a `winshell use` whose build failed, or whose other shell did not stop. Also a widget install, update or remove that fails: the build failed, the widget needs a newer Winshell, the widget or its source is already installed, the update's ref cannot be read, `git` or the .NET SDK is missing, or no installed widget or source has that name. |
| 2 | An unknown command or option, an option that needs a value without one, a spare argument, a `--pid`, `--tail`, `--fps` or `--screen` that is not a whole number, a `--for` that is not a time, an `ipc`, `inspect` or `widget` command missing its words, an `inspect record` without `--for`, with a file that doesn't end in `.mp4`, or with both `--screen` and a window, an `inspect scroll` without a number of steps from 1 to 100 or -1 to -100, an `inspect key` with keys it can't read or with the Windows key, a `--right` on any `inspect` command but `press`, a `--project` or `--yes` on `widget list` or `widget remove`, a `--project` on `widget update`, a widget source that is not a repository or a path, a `winshell new` name that is not a starter or example, or a folder that isn't empty, or a `winshell use` name that is not one folder name. |

`--pid`, `--tail` and `--screen` need a whole number, and `--fps` one above 0. Anything else prints a message such as `--pid needs a whole number.` and exits with code 2.

An unexpected failure of any command but `run` prints `winshell <command>: <message>` and exits with 1. `-v` adds the stack trace. Starting a shell still writes a crash report.

## Environment variables

| Variable | What it does |
| --- | --- |
| `WINSHELL_CONFIG_DIR` | The folder configurations load from, instead of `%APPDATA%\winshell`. |
| `WINSHELL_LOCAL_DIR` | The folder for logs, crash reports, each shell's data, state and cache, and clones of installed widgets, instead of `%LOCALAPPDATA%\winshell`. |
| `WINLAND_PIPE` | The named pipe [`Winshell.Winland`](winshell-winland.md) connects to, instead of `winland-<session id>` and then `winland`, which it tries in that order when the variable isn't set. |

## Files

| What | Where |
| --- | --- |
| Configurations | `%APPDATA%\winshell`, or `WINSHELL_CONFIG_DIR` |
| The chosen configuration | `chosen.txt` in the config folder: the name `winshell use` recorded |
| A project's program | `bin\shell` under the project folder |
| Per-shell data, state and cache | `%LOCALAPPDATA%\winshell\data\by-shell\<shell id>`, and the same under `state` and `cache`. They are `Shell.DataDir`, `Shell.StateDir` and `Shell.CacheDir`. |
| Instance logs | `%LOCALAPPDATA%\winshell\run\<instance id>\log.txt`. A shell that exits cleanly keeps that folder, with `exitedAt` set in `instance.json`, so `winshell log` can still print it. A later start deletes a stopped folder once it is a day old, including a folder that has no `instance.json`. |
| Crash reports | `%LOCALAPPDATA%\winshell\crashes`, one text file per crash. Nothing is sent anywhere. See [troubleshooting](../troubleshooting.md#winshell-crashed). |
| Run at sign-in | Unpackaged: the `Winshell` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, written only by `install.ps1 -AutoStart` or when a shell sets [`StartupService.RunAtSignIn`](winshell-session.md#startupservice). That entry passes `--no-build`. Packaged: the package's startup task, not that value. |

The console writes `LEVEL category: message`. The log file writes that same line with `HH:mm:ss.fff` in front. `INFO` and `WARN` have a leading space, on both. The levels are `DEBUG` (with `-v`), `INFO`, `WARN`, `ERROR` and `FATAL`.
