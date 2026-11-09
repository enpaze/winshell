# Winshell

Winshell is a toolkit for writing a Windows 11 shell. It ships no shell of its own. A configuration is a .NET project that builds into one program and describes the windows it shows.

## The shell

**Shell**:
The desktop shell one configuration describes, running as one process.
_Avoid_: app, host, session

**Configuration**:
The .NET project that defines a shell, built into one program.
_Avoid_: theme, skin, script

**Shell id**:
The name that identifies a configuration across runs.
_Avoid_: instance id, path

**Instance**:
One running process of a shell. A reload replaces it. The CLI lists, logs, and kills instances.
_Avoid_: generation, session

**Windows session**:
The signed-in Windows user session the shell runs in. The shell can ask Windows to lock it, sign out, restart, shut the PC down, sleep, or hibernate. Windows locks and unlocks the session and shows its own prompts.
_Avoid_: logon session, user session

**Reload**:
Building the configuration and replacing the running instance with the result.
_Avoid_: restart, refresh, generation

**Notice**:
Winshell's own window, drawn like a Windows 11 notification, for a configuration that did not build or a failure Winshell reports. It is Winshell's, not the configuration's.
_Avoid_: toast, popup, flyout

**Crash report**:
A text file Winshell writes on this PC when the shell itself fails: where, the exception, and the end of the log. A project that did not build is a build error, not a crash.
_Avoid_: dump, telemetry

## What it shows

**Window**:
A native window the shell places on the desktop, such as a panel or a popup window.
_Avoid_: form, view

**Panel**:
A window anchored to an edge of a screen. It can reserve that edge.
_Avoid_: taskbar, dock, layer surface

**Bar**:
A panel along one edge of every screen, with start, center and end widget areas. A shell can have several.
_Avoid_: taskbar (that is Explorer's), panel bar

**Popup window**:
A frameless window placed next to another window.
_Avoid_: popup, flyout

**Flyout**:
A control shown over a window, in that window's overlay. It is not a window of its own.
_Avoid_: popup, popup window, dialog

**Focus**:
Whether a window takes the keyboard, and with it activation from the app in front. A panel takes focus only when it is focusable; a popup window only while it grabs focus; a floating window always.
_Avoid_: activation, foreground

**Taskbar**:
Explorer's own bar along the bottom of each screen, the Windows taskbar. A shell's own is a bar.
_Avoid_: dock, task list

**Preview**:
The live image of an app window the taskbar shows over its button.
_Avoid_: thumbnail (that is the DWM API it uses), peek

**Preferences**:
The choices a user makes for one shell: its configuration's own, and each component's.
_Avoid_: settings (that is Windows Settings)

**Preferences file**:
The user's one TOML file for a shell, with a section for each component. It survives shell updates.
_Avoid_: placement file, layout file, widget config

**Component**:
A named part of a shell with its own section in the preferences file, such as a bar or a widget area.
_Avoid_: module, plugin

**Screen**:
One monitor on the desktop.
_Avoid_: display, output

**Work area**:
The part of a screen that windows maximize into: the screen without the taskbar and the edges panels reserve.
_Avoid_: available geometry, usable area

**Scale**:
How many physical pixels a screen draws for one logical pixel, from its DPI. 144 DPI is a scale of 1.5.
_Avoid_: zoom, DPI factor

**Backdrop**:
What shows through the transparent parts of a window: nothing, or a Windows 11 material.
_Avoid_: blur, wallpaper, effect

**Control**:
A WinUI control.
_Avoid_: widget (that is placed in a widget area), item

**Watch**:
A block of a control's code that runs again whenever a service property or list it read changes, while the control is loaded.
_Avoid_: binding, subscription, observer

**Widget**:
A piece of UI a widget area creates by name from a placement's settings. It can come from the configuration, an example, or a separate project; it is not the Windows 11 Widgets board.
_Avoid_: module, applet, plugin

**Widget area**:
A control a shell author puts anywhere in a window. It shows its placements in order, in one orientation, and styles them.
_Avoid_: slot, zone

**Surface**:
What a bar, a dock or any composition is painted on: the style's background, in the window's theme, behind its widget areas.
_Avoid_: backdrop (that is the window's material), background

**Placement**:
One widget in a widget area, with its own settings. One widget can have several placements.
_Avoid_: instance (that is a running shell process), item

**Theme**:
Light or dark, whether a contrast theme is on, the accent color and the system colors, as Windows reports them. A shell can choose light or dark and its own accent on top; a contrast theme still wins.
_Avoid_: skin, palette

**Style**:
The look a shell gives its bars, widget areas, widgets and popups: colors, font and slot size. It is the shell's, whatever the theme is.
_Avoid_: theme (that is Windows'), skin

## What it reads from Windows

**App window**:
A top-level window of another program, as the taskbar and Alt+Tab list it.
_Avoid_: toplevel, client

**Virtual desktop**:
A desktop Task View lists and can switch to. An app window is on one of them.
_Avoid_: workspace

**App**:
A program Start lists: a shortcut or a packaged app.
_Avoid_: desktop entry, application

**Start**:
The launcher opened from the Windows key or the taskbar Start button.
_Avoid_: start menu, app drawer

**Playback device**:
A Windows audio endpoint that plays sound, such as speakers or headphones.
_Avoid_: sink, audio sink, render device

**Recording device**:
A Windows audio endpoint that records sound, such as a microphone.
_Avoid_: source, audio source, capture device

**Audio session**:
One program's volume and mute on a playback or recording device, as the volume mixer lists it.
_Avoid_: stream, sink input

**Media session**:
A program playing media that Windows lets the shell see and control.
_Avoid_: player, MPRIS player

**Power mode**:
The slider Windows offers for Best power efficiency, Balanced, or Best performance.
_Avoid_: power profile, overlay scheme, power plan

**Monitor brightness**:
The brightness setting of a connected monitor.
_Avoid_: backlight

**CPU usage**:
How busy the processors are: the share of time all cores together spent busy, from 0 to 100 %. Task Manager calls it CPU.
_Avoid_: load, CPU load, utilization

**Tray icon**:
An app's icon in the notification area, which the app adds, changes and removes itself.
_Avoid_: status notifier item, SNI, indicator

**Overflow**:
The hidden icons flyout behind the taskbar's chevron. Settings chooses which tray icons the taskbar shows; the rest are in the overflow.
_Avoid_: hidden icons (an app can also hide its own icon), drawer

**Hotkey**:
A key chord or a short sequence of chords that works anywhere in Windows, such as `Ctrl+Shift+K` or `Win`.
_Avoid_: global shortcut, keybind, accelerator, bind

**Keyboard layout**:
One way to type that Windows has installed. It has a short label, such as EN, and the name Windows shows for that layout, such as United States-International. The current layout is the foreground window's. Switching reaches that window, as Win+Space does.
_Avoid_: input language, input method, IME

**Wallpaper**:
The image Windows draws on the desktop, how that image is fitted, and the color behind it. Each screen can have its own image. Windows keeps it, including after the shell exits.
_Avoid_: background, desktop background, backdrop

**Privacy use**:
An app Windows reports as using the microphone, the camera, or location right now. A desktop app has the program's path. The shell's own microphone meter is one of these apps, and it is marked so a configuration can tell it is this shell.
_Avoid_: permission, consent, recent activity

## The language

**Namespace**:
A named group of the public API, such as `Winshell.Media`.
_Avoid_: module, package, library
