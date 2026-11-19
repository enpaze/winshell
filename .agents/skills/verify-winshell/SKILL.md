---
name: verify-winshell
description: Drive a real Winshell shell on this Windows desktop with `winshell inspect` and capture proof. Use it to confirm that a change to a winshell command, a Shell service, a widget or a building block works in a running shell, to run the feature map's drivers, or to add a feature to the map.
---

# Verify Winshell

Winshell has no shell of its own. An author writes a shell in C#, and `winshell.exe` builds it, starts it and talks to it. To see a change working, build `winshell` from this checkout, start a shell that shows the change in a private session, and drive it with `winshell inspect`. `inspect` reads the running shell from inside: `tree`, `screenshot`, `record`, `click`, `hover` and `type` need no mouse and no keyboard, and they do not move the user's pointer or focus.

[The feature map](features/README.md) lists every user-facing feature with the steps that prove it. Read its README before you drive anything. All the helpers below are in `scripts/`; run them from the repository root with PowerShell 7.

## Launch

```powershell
pwsh .agents/skills/verify-winshell/scripts/launch.ps1 [-Shell features|starter|omarchy-style] [-Winshell <exe>] [-Session <new folder>]
```

- It builds `src/Winshell` in Release, unless `-Winshell` names an exe. It makes a session folder, `%TEMP%\winshell-verify\<time>-<id>` by default, with `config`, `local` and `evidence` folders and a `session.json`.
- It copies the shell into `config`, points it at this checkout's libraries, and starts it with `winshell run -d` and private `WINSHELL_CONFIG_DIR` and `WINSHELL_LOCAL_DIR`.
- The shell is ready when `launch.ps1` prints `Ready: the <shell> shell is running, pid <pid>.`, then the session folder. Ready means `winshell list -j` lists the shell and `winshell inspect tree` shows at least one window, each with content. A first build takes a few minutes.
- Choose the shell that shows your change. `features` shows every service and building block. `starter` is what `winshell new starter` copies. `omarchy-style` is the example in `examples/omarchy-style`.
- Teardown is `cleanup.ps1`, below.

`launch.ps1` refuses while another verification run's shell is live on this desktop, and names that run's session folder. The runs share the desktop and the foreground, so one runs at a time. End a stale run of your own with `cleanup.ps1 -Session <its folder>`, and wait for one that is not yours.

## Doctor

```powershell
pwsh .agents/skills/verify-winshell/scripts/doctor.ps1 -Session <folder>
```

It is read-only. Run it first, and again whenever anything looks off. It prints one `OK` or `FAIL` line for each check, and exits with 1 when one fails:

- `winshell --version` runs, and no source under `src`, `templates`, `examples` or `shell` changed since the session began. If one did, clean up and launch again.
- `winshell list -j` lists the session's shell, and its process runs from the session's `config` folder. A `NOTE` says when a `reload` or `run` replaced the pid that `launch.ps1` printed.
- `winshell inspect --pid <pid> tree` shows windows, each with content.
- No other verification run is live on this desktop.

## Drive

Run a mapped feature, a kind, or the whole map. `drive.ps1` makes its own session, starts each shell the chosen features need, runs their steps, and tears everything down:

```powershell
pwsh .agents/skills/verify-winshell/scripts/drive.ps1 -Id service.audio,block.popup-window
pwsh .agents/skills/verify-winshell/scripts/drive.ps1 -Kind widget
pwsh .agents/skills/verify-winshell/scripts/drive.ps1
pwsh .agents/skills/verify-winshell/scripts/drive.ps1 -List
pwsh .agents/skills/verify-winshell/scripts/drive.ps1 -Check
```

- It prints `PASS <id>  <evidence>` or `FAIL <id>  step <n>: <expected> / <what it got>` for each feature, then `<n> of <m> features passed. Evidence: <folder>`, and exits with 1 when one fails.
- `-Check` reads the map and checks its shape without building or starting anything. Every run checks the map first.
- With warm build caches the whole map takes about 2 minutes. On a cold machine, such as a CI runner, most of the time goes to the shells' first builds and to `winshell dev`'s `dotnet watch` build.

To drive a launched shell by hand, run each `winshell` command through the session, which sets its private folders:

```powershell
$s = '<session folder>'
pwsh .agents/skills/verify-winshell/scripts/winshell.ps1 -Session $s list
pwsh .agents/skills/verify-winshell/scripts/winshell.ps1 -Session $s inspect --pid <pid> tree
pwsh .agents/skills/verify-winshell/scripts/winshell.ps1 -Session $s inspect --pid <pid> screenshot "$s\evidence\window.png" FeatureWindow
pwsh .agents/skills/verify-winshell/scripts/winshell.ps1 -Session $s inspect --pid <pid> click features.click
pwsh .agents/skills/verify-winshell/scripts/winshell.ps1 -Session $s inspect --pid <pid> record "$s\evidence\window.mp4" --for 5s FeatureWindow --frames "$s\evidence\frames"
```

- Name elements by automation id, `x:Name` or automation name, as the feature files do. The feature shell gives each service row and building block its feature id, such as `service.audio`, and has `features.click`, `features.clicks`, `features.popup`, `features.launcher`, `features.text` and `features.typed`.
- A `[window]` is a window's content type, such as `FeatureWindow`, `BarView` or `Surface`, or its `window` number in `tree`.
- A name finds only elements that are not collapsed, as Narrator does. A name that matches more than one element acts on none, and the error lists each match.
- `inspect hover` shows a control's hover look only. A `PointerEntered` handler needs a real pointer.
- With Winland running, follow [Live UI with Winland running](../../../docs/development.md#live-ui-with-winland-running). The [command-line reference](../../../docs/reference/command-line.md) lists each `inspect` command, its output and its refusals.

### Real input

For a hover handler, a press, keyboard focus or an animation, a person at a live session can use real input. Record in the background, `point` at the element, then look at the contact sheet:

```powershell
$record = Start-Process pwsh -NoNewWindow -PassThru -ArgumentList '.agents/skills/verify-winshell/scripts/winshell.ps1', '-Session', $s, 'inspect', '--pid', <pid>, 'record', "$s\evidence\hover.mp4", '--for', '5s', 'FeatureWindow', '--frames', "$s\evidence\hover"
Start-Sleep -Seconds 1
pwsh .agents/skills/verify-winshell/scripts/winshell.ps1 -Session $s inspect --pid <pid> point features.click
pwsh .agents/skills/verify-winshell/scripts/winshell.ps1 -Session $s inspect --pid <pid> leave
$record.WaitForExit()
```

Then read `hover\contact-sheet.png`. `focus`, `press [--right]`, `scroll` and `key` work the same way.

- The mouse ones move the user's real cursor, and `key` presses real keys.
- Every one refuses while anyone has used the PC in the last 3 seconds, and before each mouse event they check that no other window is on top at the element.
- Run them only in a live session that a person is watching and agrees to, never at someone's active desktop and never unattended.
- The feature map never sends real input: it proves `focus`, `point`, `leave`, `press`, `scroll` and `key` only by a refusal that sends nothing. A test does not inject input of its own, but may run these commands.

## Evidence

Evidence goes to the session's `evidence` folder and survives cleanup:

- `output.txt`: every `PASS` and `FAIL` line of a `drive.ps1` run.
- `<feature id>\steps.txt`: each command a step ran, its exit code, its output and the step's verdict.
- `<feature id>\tree-<n>.json`: the `inspect tree` an element step read.
- `<feature id>\window.png`, `window.mp4`: the screenshot and recording steps save.
- `logs\`: each shell's log.

Proof standards:

- Exercise the real user path: the command a user types, or the control a user chooses through `inspect click`. Never set state through test hooks.
- Capture the action and the resulting state. Read the value in the tree, and look at a screenshot for a visual change, rather than trusting that the code ran.
- Check side effects alongside what is visible, such as the file `inspect screenshot` writes or the folder `winshell new` creates.
- `FAIL` means the feature failed. Never weaken a step's pattern to make it pass; report the step and what it got.
- Say which shell and which entry point you drove. An entry point the feature file does not drive is not verified.

## Cleanup

```powershell
pwsh .agents/skills/verify-winshell/scripts/cleanup.ps1 -Session <folder>
```

- It kills each shell the session's `winshell list -j` lists with `winshell kill --pid`, and with `--force` when one does not exit. Then it ends any process still running from the session folder, or from the session's `winshell.exe` and started after the session began.
- It copies the shells' logs into `evidence\logs`, then removes `config` and `local`. It keeps `evidence` and `session.json`.
- It prints a `FAIL` line for anything it had to end or could not remove, and exits with 1.
- It never kills by process name. `drive.ps1` cleans up its own session, including after a failure or Ctrl+C.

Run it after every launched session, and after every failed attempt, so no shell keeps a window, the top edge or a hidden taskbar.

## Helpers

| Helper | What it does |
| --- | --- |
| `scripts/launch.ps1 [-Shell <shell>] [-Winshell <exe>] [-Session <folder>]` | Builds winshell, makes a session, starts a shell and waits until it is ready. |
| `scripts/doctor.ps1 -Session <folder>` | Read-only checks that the session's shell is worth driving. |
| `scripts/winshell.ps1 -Session <folder> <words>` | Runs the session's `winshell` with its private folders. |
| `scripts/drive.ps1 [-Id <id>[,<id>]] [-Kind <kind>] [-List] [-Check] [-Winshell <exe>] [-Session <folder>]` | Checks the map, then drives the chosen features in their own session. |
| `scripts/cleanup.ps1 -Session <folder>` | Stops the session's shells and removes everything but its evidence. |

`Session.ps1` and `FeatureMap.ps1` hold the code the helpers share. `shell/` is the feature shell.

## Change the map

- An added or removed command, service, widget or building block needs its feature file, in the shape [the README](features/README.md) describes, and its line in the README's index. For a service or a building block, also add a row or element to the feature shell in `shell/` with the feature's id as its automation id.
- Then run `drive.ps1 -Check` and `drive.ps1 -Id <id>`.
- `FeatureMapTests` in `tests/Winshell.Tests` fails when the map misses a feature or names one that winshell does not have. The `features` job in CI runs `drive.ps1`, which fails on a malformed file and on a failing step.
- `.claude/skills/verify-winshell/SKILL.md` lets Claude Code find this skill. It repeats this file's front matter, and `tests/Scripts/VerifyWinshell.Tests.ps1` fails when the two differ.
