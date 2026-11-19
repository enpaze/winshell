# Winshell verification map

This folder is the maintained source for verifying what a Winshell user can reach: each `winshell` command, each service on `Shell`, each widget the starter and the examples have, and each building block a user sees. Read this index before you drive a shell, then use the matching feature file as the recipe. `scripts/drive.ps1` runs every file's steps and prints `PASS` or `FAIL` for each feature.

## Baseline preconditions

- A Windows 11 desktop session with the .NET SDK that `global.json` names. Nobody else is using the desktop, because the shells open windows on it.
- One verification run at a time on a desktop. `launch.ps1` and `drive.ps1` refuse while another run's shell is live, and name its session folder.
- Each run has its own session folder with private `WINSHELL_CONFIG_DIR` and `WINSHELL_LOCAL_DIR`. The helpers set both on every `winshell` they start. Never drive a shell that a run of your own did not start.
- With Winland running, follow [Live UI with Winland running](../../../../docs/development.md#live-ui-with-winland-running). Never add the window rule or reload Winland.
- While their features run, the `starter` shell's bar reserves the top edge of each screen, and the `omarchy-style` shell hides Explorer's taskbar.

## Shells

| Shell | Source | What it shows |
| --- | --- | --- |
| `none` | No shell | Commands that need no running shell. |
| `features` | [`../shell`](../shell/Program.cs) | One window with a row for each service and an element for each building block, each with the feature's id as its automation id, plus `features.click`, `features.clicks`, `features.popup`, `features.launcher`, `features.text` and `features.typed`. It registers the IPC target `features` with the function `echo`, the property `name` and the signal `ticked`, which it raises once a second. |
| `starter` | [`templates/winshell`](../../../../templates/winshell/Program.cs) | The project `winshell new starter` copies: a bar with a clock and a battery. |
| `omarchy-style` | [`examples/omarchy-style`](../../../../examples/omarchy-style/Program.cs) | A top bar in the style of Omarchy, with its own widgets. |

The driver copies each shell into the session's shells folder, points it at this checkout's libraries instead of the NuGet package, and starts it with `winshell run -d`.

## Driving conventions

- Drive with `winshell inspect` only. `tree`, `screenshot`, `record`, `click`, `hover` and `type` read and act inside the shell, through its automation peers. They need no mouse and no keyboard, and they do not move the user's pointer or focus. Use no other UI automation and no screenshot from another tool.
- No step sends real input. `inspect focus`, `point`, `leave`, `press`, `scroll` and `key` move the real cursor or press real keys, so their features prove only a refusal that sends nothing.
- Run a command under test as a user types it, through the session's `winshell`: `scripts/winshell.ps1 -Session <folder> <words>`.
- Find an element by its automation id, `x:Name` or automation name. Never use a position or the order of the tree.
- Start every recipe from a freshly started shell. A step that ends the shell, such as `kill` or `reload`, leaves the next step to start it again.

## Feature entry contract

A feature file is named `<kind>.<slug>.md`. The kind is `command`, `service`, `widget` or `block`, and the slug is the inventory name in lowercase with hyphens, such as `command.inspect-click.md` for `winshell inspect click` or `service.hotkey.md` for `Shell.Hotkey`.

Each file starts with an H1 title and one paragraph that says what the user sees. It then uses exactly four H2 sections in this order.

1. `Sub-features` lists short IDs with one line for each behavior, as ``- `short-id` what it does``.
2. `How to get to it (user POV)` lists every entry point a user has: a command they type, a member of `Shell` their shell calls, or a control they choose.
3. `Driving it with winshell inspect` starts with `Preconditions:` and a bullet that names the shell, ``- Shell: `features`.``, then pairs each step with a labeled bullet, ``- **Label.** <what the user does>. Run `<command>`. <what proves it>.``, and ends with one ` ```json ` block that holds the steps the driver runs, in the same order.
4. `Gotchas` lists traps that can waste or invalidate a verification run.

Keep implementation details out of the map. Name only user paths, stable handles, required state, commands and observable proof. A step bullet names its command exactly as the driver runs it: `winshell` and the step's words, or `winshell inspect --pid {pid} tree` for an element step.

A step is one of two objects. Each takes `within`, the seconds to retry it until it passes.

| Step | What it checks |
| --- | --- |
| `{"winshell": [words], "exit": 0, "output": "<regex>", "file": "<path>"}` | Runs `winshell` with those words. The exit code is one of `exit`, 0 by default, the output matches `output`, and `file` exists and is not empty. `"background": true` leaves it running until the feature ends. `"for": <seconds>` stops it after that long and checks only the output. |
| `{"element": "<name>", "window": "<window>", "has": {"<property>": "<regex>"}}` | Runs `winshell inspect --pid {pid} tree` and finds exactly one element with that `x:Name`, automation id or automation name, in the first window whose number, kind or content type is `window`, and matches each property. A property the element doesn't have is empty. It retries for 10 seconds unless `within` says otherwise. |

`{pid}`, `{shell}` and `{scratch}` in a step stand for the running shell's process id, its project folder, and the feature's evidence folder. A step that uses `{pid}` starts the feature's shell again when none is running.

`scripts/drive.ps1 -Check` fails on a file that breaks this contract, and on an index below that misses a file. `FeatureMapTests` fails when a command, service, widget or building block has no file, or a file names one that winshell does not have.

## Proof and skip reporting

- A feature is proved by its `PASS` line in `output.txt` and its folder in the session's `evidence` folder: `steps.txt` with each command, exit code, output and verdict, `tree-<n>.json` with the tree an element step read, and any screenshot or recording a step saved.
- A visual change also needs a screenshot you looked at. Check values in the tree rather than trusting that the code ran.
- Report a `FAIL` line with its step and what the driver got. Never weaken a step's pattern so that it passes. When the machine lacks what a step needs, such as a battery or a Bluetooth adapter, the step accepts both states, and the feature's `Gotchas` say which one CI sees.
- Do not report a feature as verified through an entry point its file does not drive.

## Features

- [Bar](./block.bar.md) is a panel along one edge of every screen with start, center and end widget areas, driven on the starter's top bar.
- [CaptureView](./block.capture-view.md) shows the live frames of a capture session of a window or a screen.
- [FloatingWindow](./block.floating-window.md) is a normal window with a title bar and a size.
- [Launcher](./block.launcher.md) is a search box a hotkey opens, which finds apps and commands and runs the chosen one.
- [PanelWindow](./block.panel-window.md) is a frameless window on one edge of a screen that can reserve its thickness along that edge.
- [PopupWindow](./block.popup-window.md) is a frameless window placed next to another, opened here by the feature window's `Popup` button.
- [Surface](./block.surface.md) paints the style's background behind its content and follows the theme.
- [WidgetArea](./block.widget-area.md) makes a widget for each placement and passes it its settings.
- [WindowThumbnail](./block.window-thumbnail.md) shows a live preview of another window, drawn by Windows' window manager.
- [winshell dev](./command.dev.md) runs a project under `dotnet watch` and applies each saved edit to the running shell.
- [winshell doctor](./command.doctor.md) checks this PC's setup for a shell and prints the checks, as JSON with `-j`.
- [winshell help](./command.help.md) prints the command summary, or one command's usage, without running it.
- [winshell inspect click](./command.inspect-click.md) activates an element through its automation peer, as Narrator does.
- [winshell inspect focus](./command.inspect-focus.md) presses real Tab keys until keyboard focus reaches an element.
- [winshell inspect hover](./command.inspect-hover.md) puts a control in its `PointerOver` look without moving the pointer.
- [winshell inspect key](./command.inspect-key.md) presses real keys or chords in a shell window.
- [winshell inspect leave](./command.inspect-leave.md) puts the cursor back after `point`.
- [winshell inspect point](./command.inspect-point.md) moves the real pointer onto an element and holds it there.
- [winshell inspect press](./command.inspect-press.md) presses a real left or right mouse button on an element.
- [winshell inspect record](./command.inspect-record.md) records a window or a screen as an MP4.
- [winshell inspect screenshot](./command.inspect-screenshot.md) saves a window or an open popup as a PNG.
- [winshell inspect scroll](./command.inspect-scroll.md) turns the real mouse wheel over an element.
- [winshell inspect tree](./command.inspect-tree.md) prints the shell's open windows and their elements as JSON.
- [winshell inspect type](./command.inspect-type.md) replaces the text in a text box without pressing keys.
- [winshell ipc call](./command.ipc-call.md) calls a function a shell registered and prints what it returns.
- [winshell ipc get](./command.ipc-get.md) prints the current value of a property a shell registered.
- [winshell ipc listen](./command.ipc-listen.md) prints each signal a shell raises until Ctrl+C.
- [winshell ipc show](./command.ipc-show.md) lists a shell's IPC targets with their functions, properties and signals.
- [winshell ipc wait](./command.ipc-wait.md) waits for one signal and prints its first argument.
- [winshell kill](./command.kill.md) stops a shell and waits until it has exited.
- [winshell list](./command.list.md) lists the running shells, as JSON with `-j`.
- [winshell log](./command.log.md) prints a shell's log, or the end of it with `-t`.
- [winshell new](./command.new.md) lists the starter and the examples, or copies one into a folder.
- [winshell reload](./command.reload.md) rebuilds a project and replaces its running shell with a new process.
- [winshell run](./command.run.md) builds a project and starts and supervises its shell.
- [winshell use](./command.use.md) chooses the shell `winshell run` starts, and switches to it.
- [winshell update](./command.update.md) installs the newest build of the saved channel.
- [App windows](./service.app-windows.md) lists the open app windows and the active one.
- [Apps](./service.apps.md) lists the apps Start lists, and searches and launches them.
- [Audio](./service.audio.md) reads and sets the playback and recording devices, sessions and volume.
- [Bluetooth](./service.bluetooth.md) reads the adapter and its devices, and pairs and connects them.
- [Brightness](./service.brightness.md) reads and sets the brightness of the monitors that support it.
- [CPU](./service.cpu.md) reads how busy the processor is.
- [Explorer taskbar](./service.explorer-taskbar.md) hides and shows Explorer's taskbar.
- [Hotkeys (Hotkey)](./service.hotkey.md) registers a global hotkey.
- [Jump lists](./service.jump-lists.md) reads an app's recent and frequent destinations.
- [Keyboard layouts](./service.keyboard-layouts.md) reads and switches the input languages.
- [Media](./service.media.md) reads and controls the media sessions Windows shares.
- [Network](./service.network.md) reads connectivity, adapters and Wi-Fi, and connects to networks.
- [Idle monitor (NewIdleMonitor)](./service.new-idle-monitor.md) reports when the user is idle.
- [Notifications](./service.notifications.md) reads and dismisses notifications, and sends toasts.
- [Power](./service.power.md) reads the battery and the power source.
- [Power mode](./service.power-mode.md) reads and sets the power mode.
- [Privacy](./service.privacy.md) lists the apps using the microphone, camera and location.
- [Recent documents](./service.recent-documents.md) searches recent documents.
- [Session](./service.session.md) locks, sleeps, signs out and shuts down.
- [Startup](./service.startup.md) reads and sets whether the shell runs at sign-in.
- [Tray](./service.tray.md) lists the tray icons apps add and passes clicks to them.
- [Virtual desktops](./service.virtual-desktops.md) lists and switches the virtual desktops.
- [Wallpaper](./service.wallpaper.md) reads and sets the wallpaper.
- [Winland](./service.winland.md) reads Winland's monitors, workspaces and windows.
- [omarchy-style audio widget](./widget.omarchy-style-audio.md) shows the playback device and its volume, and opens Sound settings.
- [omarchy-style battery widget](./widget.omarchy-style-battery.md) shows the charge and its state, and opens Power & battery settings.
- [omarchy-style bluetooth widget](./widget.omarchy-style-bluetooth.md) shows whether Bluetooth is off, on or connected, and opens Bluetooth settings.
- [omarchy-style clock widget](./widget.omarchy-style-clock.md) shows the day and the 24-hour time, in an optional time zone.
- [omarchy-style network widget](./widget.omarchy-style-network.md) names the connection or says `Disconnected`, and opens network settings.
- [omarchy-style screen widget](./widget.omarchy-style-screen.md) names the bar's monitor and opens Display settings.
- [omarchy-style tray widget](./widget.omarchy-style-tray.md) shows the tray icons on the primary screen, with a chevron for the overflow.
- [omarchy-style windows widget](./widget.omarchy-style-windows.md) shows the Windows glyph at the start of the bar.
- [omarchy-style workspaces widget](./widget.omarchy-style-workspaces.md) shows Winland's workspaces for the bar's screen.
- [starter battery widget](./widget.starter-battery.md) shows the battery's charge as a percentage.
- [starter clock widget](./widget.starter-clock.md) shows the short time in the Windows regional format.
