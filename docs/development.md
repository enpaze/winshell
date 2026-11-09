# Development

How to build, check and release Winshell. [CONTRIBUTING.md](../CONTRIBUTING.md) has the test rules and how a change lands, and the [architecture rules](architecture.md) define the assembly boundaries and feature ownership.

## Prerequisites

- Git
- PowerShell 7
- Pester 5.6.1, for the PowerShell tooling tests
- .NET 10 SDK
- Node.js, for `npx` when installing or updating agent skills
- Claude Code 2.1.281 or later, only for an agent on Claude Code: an older one may not read `AGENTS.md`, and then starts without the repo's instructions and nothing says they are missing
- The Windows SDK, for `makeappx.exe`, only to run `scripts/package.ps1`
- GitHub CLI (`gh`), only when publishing a draft release outside GitHub Actions

Two Windows features need more, and neither is needed to build or run automated tests:

- Reading Windows notifications requires listener permission and a package identity declaring `userNotificationListener`.
- Native toast sending requires an installed Windows AppUserModelID.

The unpackaged CLI does not provision these identities; the Microsoft Store package declares the listener capability (see [Releasing](#releasing)).

## First checkout

```sh
git clone https://github.com/enpaze/winshell.git
cd winshell
pwsh -NoProfile -Command "Install-Module Pester -RequiredVersion 5.6.1 -Scope CurrentUser -Force -SkipPublisherCheck"
pwsh scripts/check.ps1
```

## Build from source

You need Git and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

```powershell
git clone https://github.com/enpaze/winshell.git
cd winshell
dotnet publish src\Winshell -c Release -o "$env:LOCALAPPDATA\winshell-dev"
```

That puts `winshell.exe` in `%LOCALAPPDATA%\winshell-dev`, away from the folder the PowerShell installer uses. Add that folder to your `PATH` to run `winshell` from anywhere. Like a release, the build carries its own .NET runtime.

- **To update a source build**, close Winshell, pull the newer source and publish it over the same folder.
- **To uninstall one**, close Winshell, delete the `Winshell` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` if your shell set it, and delete its folder. Your configuration is in `%APPDATA%\winshell`, and logs, state and crash reports are in `%LOCALAPPDATA%\winshell`; delete them too if you don't want to keep them.

## Run the shell from source

```powershell
dotnet run --project src/Winshell -- run -p ..\MyShell
```

That builds the configuration project and runs it in a child process that it supervises. `-p` names the project: a folder with a `.csproj`, or the project file. `--no-build` starts the last build and does not compile. From another terminal:

```powershell
dotnet run --project src/Winshell -- list
dotnet run --project src/Winshell -- log -t 20
dotnet run --project src/Winshell -- kill -i <id>
```

## Agent skills

Install every skill `skills-lock.json` records, for Codex and Claude Code. Agents do this first in every session:

```sh
pwsh scripts/install-skills.ps1
```

It installs each source at the ref the lock pins, as copies. The CLI's `experimental_install` links instead, and on Windows that leaves the external skills linked to no agent.

Installed copies in `.agents/skills/` and `.claude/skills/` are ignored by Git; keep `skills-lock.json` tracked. Every skill installs from the source repository the lock records, `test-audit` included, which comes from OpenClaw. Skills become available in a new agent session.

## Start a task

Fetch before creating a task's worktree. Use its issue number in the branch and folder names:

```powershell
git fetch origin main
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch main.' }
git worktree add -b agent/codex/123-short-description ../winshell-123 origin/main
```

In an existing task worktree, fetch again and run `git rev-list --left-right --count HEAD...origin/main`. It prints commits unique to the task, then commits unique to `main`. A nonzero second count means the task is behind; update its base before editing. An intentionally stacked task starts from its predecessor instead.

T3 Code's Winshell project uses worktrees and automatic pulls, with a **Check task base** script that fetches and reports divergence on creation. It stops on a stale base without resetting files. Other tools should use the commands above.

For agent shell commands, use profile-free PowerShell (`pwsh -NoProfile`, or `login: false` in Codex's command tool). Search with `rg` and read the relevant file section or diff; set a bounded output limit so unrelated output does not hide failures.

### Pull request stacks

Prefer small, focused PR stacks for related work. Each layer still has its own issue, branch, worktree and PR, and must build and be reviewable on its own. Create a dependent layer's worktree from its predecessor's branch, then adopt the existing branches in bottom-to-top order with `gh stack init`.

Install the extension once with `gh extension install github/gh-stack`.

| Command | When to use it |
| --- | --- |
| `gh stack init <bottom-branch> [<next-branch> ...]` | Start a stack or adopt existing branches, ordered from the branch targeting `main` to the top layer. |
| `gh stack add <next-branch>` | Create the next branch on top of the stack. Return the current worktree to its previous branch, then use `git worktree add <folder> <next-branch>` for the new layer before editing. |
| `gh stack view` | Inspect the layers and their PR status. |
| `gh stack submit` | Fill in each PR's title and description in the interactive editor, then push and publish the stack. Follow the [PR template](../CONTRIBUTING.md#pr-description). Run the required checks in each affected worktree before this command, since it pushes. |
| `gh stack rebase` | Fetch the latest trunk and rebase each layer onto its updated parent. Resolve conflicts by intent, as [Conflicts and merging](../CONTRIBUTING.md#conflicts-and-merging) describes. |
| `gh stack push` | Push updated layers together with explicit leases. Run the [checks for the change](#checks-by-change) in each affected worktree after rebasing and before pushing. |
| `gh stack merge <stack-number> --squash` | Merge the ready stack in one operation, with one squash commit per PR. Every layer must meet its required checks and reviews. This avoids intermediate rebases and repeated CI runs between individual merges. |

For automation without the interactive editor, create each PR with `gh pr create --base <parent-branch> --head <branch> --title <title> --body-file <file>`, using `main` for the bottom layer. Then link the PRs in bottom-to-top order with `gh stack link <bottom-pr> <next-pr> ...`. To extend an existing stack, use `gh stack link <stack-number> <new-pr>`.

After the merge, confirm each layer's issue is closed with `gh issue view <issue> --json state --jq .state`. GitHub sometimes leaves one open; `close-fixed-issues` closes it a minute or so later. If it stays `OPEN`, close it with `gh issue close <issue> --comment "Fixed by #<pr>."`.

## Formatting and style

- **C# whitespace** follows `.editorconfig` and the SDK formatter. Run `pwsh -NoProfile scripts/format.ps1` to apply formatting, or add `-VerifyNoChanges` to check without editing source files. The full local check and CI run verification; commits have no formatting hook.
- **Code style and analyzers** are the build's. Builds enforce code-style and Roslyn analyzer warnings through `EnforceCodeStyleInBuild` and warnings as errors.
- **Names.** Test methods use sentences with underscores for spaces. Windows interop declarations keep the Windows spelling, such as `ABE_BOTTOM`, `POINT` and `SHAppBarMessage`; Winshell's own names use PascalCase.
- **XAML** keeps its existing formatting.

### What the formatter covers

`scripts/format.ps1` runs `dotnet format whitespace --folder`: folder mode, without loading projects, restoring packages or building. It requires Git, .NET and PowerShell 7.

- Git supplies the existing tracked and untracked C# files under `src/`, `tests/`, `examples/`, `templates/` and `docs/samples/`, plus `scripts/generate-reference.cs`. Ignored build outputs are excluded.
- Other standalone C# scripts are not formatted.
- Verification leaves source files, project references and the Git index unchanged. It fails on formatting drift or tool errors.

### The template build

`pwsh -NoProfile scripts/build-template.ps1` compiles a temporary copy of the template against the current source libraries, with the repository's build and style policy and warnings as errors. It does not run a shell. It requires Git, .NET and PowerShell 7.

- The original template project keeps its package reference and version placeholder.
- The full local check runs this command. CI runs it with the Shell suite's build, along with the solution's example and documentation sample projects and the reference generator's program entry point. All remain required through `ci`.
- Packed-template and live UI validation remain separate checks.

## Run one test

```powershell
dotnet test tests/Winshell.Core.Tests --filter FullyQualifiedName~DirectoryWatchTests
```

`--filter` takes a class or a test name. `Winshell.Architecture.Tests`, `Winshell.Core.Tests`, `Winshell.Services.Tests` and `Winshell.Shell.Tests` open no windows, so they're safe to run while you work, except for a test in the category `Window`, such as `FocusTakeoverTests` in the Services suite or `PanelWindowTests` in the Shell suite. `--filter Category!=Window` leaves those out.

### A windowed test

A test is windowed when it shows a window on the desktop, takes the foreground, or sends real input. `Winshell.Tests` is windowed whole, and a windowed test in any other project carries `[Trait("Category", "Window")]`. The full check leaves them all out, and CI's [test shards](#test-shards) run them on every PR.

To run one on purpose, such as a test you changed, to see it fail and then pass, follow [Live UI with Winland running](#live-ui-with-winland-running):

```powershell
dotnet test tests/Winshell.Shell.Tests --filter FullyQualifiedName~PanelWindowTests
dotnet test tests/Winshell.Services.Tests --filter Category=Window
```

## Live UI with Winland running

Winland tiles every normal window, so the windows a live run or a windowed test opens would change size and land among the user's windows. The owner's Winland configuration keeps them out of tiling with this line, which a `winland reload` keeps:

```text
windowrule = ignore, exe:(winshell|winshellw|Winshell\..*Tests|Probe|Dev)\.exe
```

- Leave the rule and the user's workspaces as they are. A run never adds the rule or reloads Winland.
- One live run or windowed test at a time on a machine: they share the desktop and the foreground.
- Test projects are their own programs, not `testhost`; `Probe` and `Dev` are their shell fixtures. Without the rule `A_floating_window_has_the_size_and_title_it_was_given` fails.
- No workspace contains what the rule ignores: an ignored window opens where Windows puts it, usually on the primary monitor, and a taskbar opens on every screen. Keep what a test opens small and short.
- `winland -j clients` must list no Winshell window, and the user's windows must be where they were when you finish.

## See what you changed

`winshell inspect` is how to verify a visual change. It reads a running shell from inside, so its `tree`, `screenshot`, `record`, `click` and `hover` need no mouse and no keyboard, and they do not move the user's pointer or focus.

Winshell has no shell of its own, so run one that shows the change: a project made with `dotnet new winshell`, as the [configuration guide](configuration.md#create-one) shows, with the change in it. A test can start the probe shell in `tests/Winshell.Tests/ProbeShell.cs` instead. Run it under "Live UI with Winland running", with private `WINSHELL_CONFIG_DIR` and `WINSHELL_LOCAL_DIR`, and pass `--pid`:

```powershell
winshell run -d -p .\MyShell                                  # build and start the shell
winshell inspect --pid <pid> tree                             # every open window's elements, sizes and values, as JSON
winshell inspect --pid <pid> screenshot out.png 0             # window 0 in the tree as a PNG, which you can then read
winshell inspect --pid <pid> click <name>                     # activate a named element through its automation peer
winshell inspect --pid <pid> record out.mp4 --for 5s 0 --frames frames
                                                              # an MP4 of the window, each frame as a PNG, and a contact sheet
```

- `winshell list` shows the process id.
- Look at the screenshot, and check values in the tree rather than trusting that the code ran.
- The tree marks the element keyboard focus is on with `"focused": true`.
- A name finds only elements that are not collapsed, as Narrator does, so a control kept collapsed beside a visible twin does not make the name ambiguous.
- `winshell inspect hover` shows a control's hover look only: a `PointerEntered` handler needs a real pointer.

### Real input

For hover, a press, keyboard focus or an animation, use real input. Record in the background, `point` at the element, then look at the contact sheet:

```powershell
$record = Start-Process winshell -NoNewWindow -PassThru -ArgumentList 'inspect', '--pid', <pid>, 'record', 'hover.mp4', '--for', '5s', '0', '--frames', 'frames'
Start-Sleep -Seconds 1                                        # capture takes a moment to start
winshell inspect --pid <pid> point TrayChevron                # the mouse stays on it: PointerEntered and the hover look run
winshell inspect --pid <pid> leave                            # the cursor goes back, and PointerExited runs
$record.WaitForExit()                                         # then read frames\contact-sheet.png
```

`focus`, `press [--right]`, `scroll` and `key` work the same way.

- The mouse ones move the user's real cursor.
- Every one refuses while anyone has used the PC in the last 3 seconds, and before each mouse event they check that no other window is on top at the element.
- Run them only in a live session, never at someone's active desktop.
- A test does not inject input of its own, but may run these commands.

The [command-line reference](reference/command-line.md) lists each command and refusal.

## Checks by change

Choose checks from the complete change against the PR's base, including renamed and deleted files. Run them again after rebasing.

| Change | Before pushing |
| --- | --- |
| Only Markdown or files under `docs/` outside `docs/samples/`, without compiled examples or generated reference changes | `pwsh -NoProfile scripts/check.ps1 -Lint`. No application build, formatting or test suite is needed. |
| A page in `docs/` (outside `docs/agents/`) or `README.md` containing or removing a C# example fence, or a generated `docs/reference/winshell*.md` page | Lint, then `pwsh scripts/check.ps1 -Tier Docs`. This builds the samples and checks the generated reference, without unrelated tests or windows. |
| Other changes, including source, scripts, workflows and build configuration | `pwsh scripts/check.ps1`, followed by the focused checks the [Jobs](#jobs) table lists for that change. |

Some changes need more than checks:

- A new or changed windowed test: run it on purpose, as [A windowed test](#a-windowed-test) describes, and see it fail before the change and pass after it.
- A changed public member of `Winshell.Shell`, `Winshell.Services` or `Winshell.Core`: update its `///` comment, run `dotnet run scripts/generate-reference.cs`, and commit the pages it writes.
- An added or changed command, prerequisite, environment variable or top-level folder: update this page or its [project structure](#project-structure).

Pages that still contain C# fences get the targeted documentation checks even when the edit changes only their prose. Deleting the last fence or a whole example page also needs those checks. The existing documentation tests own this contract; no application tests are needed just for wording changes.

CI uses `scripts/ci-changes.ps1` to make the same selection for PRs and pushes to `main`, and to decide whether the Package workflow builds (its `package` flag):

- A missing patch for a page covered by `DocsTests` conservatively selects documentation tests.
- An empty or potentially truncated file list selects the full suite.
- A rename includes its previous path, so moving a source file into `docs/` still selects full checks.

## Commands

| Command | What it does |
| --- | --- |
| `pwsh scripts/check.ps1 [-Tier Tests\|Docs\|Package] [-Seed <seed>] [-NoBuild]` | By default, builds, verifies formatting, and runs the .NET tests that open no windows and the PowerShell suites. See [The full check](#the-full-check). |
| `pwsh -NoProfile scripts/check.ps1 -Lint` | Checks Markdown links and workflows, without building. See [Lint](#lint). |
| `Invoke-Pester -Path tests/Scripts/<Name>.Tests.ps1` | Runs one of the PowerShell suites `check.ps1` runs, each named after the script or tooling it tests. None changes the checkout or GitHub. Requires Pester 5.6.1 and PowerShell 7; `Install.Tests.ps1` also runs on Windows PowerShell 5.1. |
| `pwsh -NoProfile scripts/format.ps1 [-VerifyNoChanges]` | Formats handwritten C# files, or verifies them. See [What the formatter covers](#what-the-formatter-covers). |
| `pwsh -NoProfile scripts/build-template.ps1` | Compiles the template against the current source libraries. See [The template build](#the-template-build). |
| `dotnet run scripts/generate-reference.cs` | Builds `Winshell.Shell`, `Winshell.Services` and `Winshell.Core`, then writes `docs/reference/winshell*.md` from their public types and `///` comments. Each page keeps what is above its `<!-- generated below -->` line. Commit the pages after changing a public member; `DocsTests` fails when a page is not what this writes. |
| `dotnet run scripts/render-logo.cs` | Renders `packaging/logo.svg` into the package's PNGs (the manifest sizes, their scale-125, scale-150, and scale-200 variants, and the unplated target sizes), the Store listing's PNG, and `packaging/winshell.ico`. Commit them after changing the logo. |
| `pwsh scripts/pack.ps1` | Packs the `Winshell` library and the `Winshell.Templates` package. See [Pack the library and the template](#pack-the-library-and-the-template). |
| `pwsh scripts/package.ps1` | Builds what a release ships, then checks it. See [Package a release](#package-a-release). |
| `pwsh scripts/publish-release.ps1 -Tag v1.2.3 -PackageDirectory <packages> -Repository enpaze/winshell` | Normally called by CI: uploads the six required files to an existing draft, verifies GitHub's digests, then publishes it. Requires an authenticated `gh`. Refuses an incomplete published release, never replaces an existing asset, and leaves a failed upload/verification as a draft. Use only for an approved release. |
| `pwsh scripts/install.ps1` | Installs the portable build for the current user from a GitHub release. See [The install scripts](#the-install-scripts). |
| `pwsh scripts/uninstall.ps1` | Removes that install. See [The install scripts](#the-install-scripts). |
| `pwsh scripts/pr-rules.ps1 -Title <title> -Body <body> -Path <file>` | What the `pr-rules` job runs, given a file of changed paths. It prints each problem and exits 1. |
| `pwsh scripts/pr-screenshot.ps1 <png>` | Publishes a `winshell inspect` screenshot on the `pr-screenshots` branch, which never merges, and prints the Markdown to paste into a PR. |
| `pwsh scripts/ci-changes.ps1 -Files <json> [-MaxFiles <limit>]` | Classifies GitHub changed-file JSON from the checkout root and prints the flags `ci.yml` and `package.yml` use. PR file lists have a 3000-file limit; push comparisons have a 300-file limit. CI fetches this input; local contributors normally choose the checks above directly. |
| `pwsh scripts/install-skills.ps1` | Installs every skill in `skills-lock.json` for Codex and Claude Code, each at its pinned ref, as copies. See [Agent skills](#agent-skills). |
| `npx -y skills@1.7.4 update -p` | Update the unpinned agent skills. Pinned ones (with a `ref` in `skills-lock.json`) change only when re-added with a newer `#<sha>`. |

### The full check

`pwsh scripts/check.ps1` builds with warnings as errors, verifies formatting, compiles the template against source libraries, builds `scripts/generate-reference.cs` as the program it runs as, and runs every PowerShell 7 suite in `tests/Scripts/` and the .NET tests that open no windows: every test project but `Winshell.Tests`, one after another, leaving out the category `Window`.

It opens no windows, so any number of agents can run it at once. CI runs the [windowed tests](#a-windowed-test) on every PR.

| Option | What it does |
| --- | --- |
| `-Tier Tests` | The default. |
| `-Tier Docs` | Selects only `DocsTests`. |
| `-Tier Package` | Selects only the packed shell deployment tests (`Category=Package`), which the default leaves out: it packs the library and the template, builds a shell from it and runs it. |
| `-Seed <seed>` | Replays the randomized .NET test order. |
| `-NoBuild` | Uses an existing solution build. Template and reference-program compilation still run. |

- Missing Pester 5.6.1 fails with an installation command; it is never silently skipped or downloaded.
- A run fails when a test fails, no test ran, or a test project wrote no results.
- It ends by naming each skipped test with its reason, including after a failure.

### Lint

`pwsh -NoProfile scripts/check.ps1 -Lint` runs the existing offline Markdown link/fragment check, actionlint and offline zizmor, stopping on the first failure. Run it immediately after Markdown or workflow edits, from the repo root. T3 Code also exposes it as **Check docs and workflows**.

It needs the pinned lint tools below on PATH. It does not build, run tests, require Pester, or download tools: routine linting uses installed tools and reads no network, and dependency installation is explicit.

### Local lint setup

Install these versions, matching `.github/workflows/ci.yml`, and put their executables on PATH:

- [lychee 0.24.2](https://github.com/lycheeverse/lychee/releases/tag/lychee-v0.24.2): extract the Windows archive and add the folder containing `lychee.exe`.
- [actionlint 1.7.12](https://github.com/rhysd/actionlint/releases/tag/v1.7.12): extract the Windows archive and add the folder containing `actionlint.exe`.
- [zizmor 1.31.1](https://github.com/zizmorcore/zizmor/releases/tag/v1.31.1): `uv tool install zizmor==1.31.1`, then `uv tool update-shell` if its executable folder is not on PATH. This setup command needs [uv](https://docs.astral.sh/uv/getting-started/installation/).

### Pack the library and the template

`pwsh scripts/pack.ps1` packs the `Winshell` library a configuration references, and the `Winshell.Templates` package, into `artifacts/packages/`.

- **The library** carries `Winshell.Native`, `Winshell.Core`, `Winshell.Services` and `Winshell.Shell`, and depends on `Microsoft.WindowsAppSDK.WinUI` and `Microsoft.WindowsAppSDK.Foundation` at the versions in `Directory.Packages.props`. CsWin32 generates Native's declarations at build time and is not a package consumer dependency.
- **The template package** is `dotnet new winshell`, the starter: a bar with a clock and a battery. The template references only `Winshell` and gets WindowsAppSDK through it.
- **Neither package is published to a feed.** `scripts/package.ps1` puts the library in each release.
- The Package workflow runs the `Category=Package` tests, which call `scripts/pack.ps1`.
- `-Out` chooses the folder, and `-Version` overrides the release from `.release-please-manifest.json`.

### Package a release

`pwsh scripts/package.ps1` builds what a release ships into `artifacts/`, then checks it. The same commit packs to the same bytes.

It builds:

- a self-contained, trimmed, ReadyToRun `winshell-<version>-win-x64.zip` and `-win-arm64.zip`, each with `packages/Winshell.<version>.nupkg`, the library `scripts/pack.ps1` makes for that version, next to `winshell.exe`
- `SHA256SUMS.txt`
- the winget manifests in `winget/<version>/`, and `winget-manifests.zip`
- copies of `install.ps1` and `uninstall.ps1`
- `winshell-<version>.msixbundle` for the Microsoft Store, which needs `makeappx.exe` from the Windows SDK

It checks:

- that every file a release uploads is there, and that the checksums match
- the library in each zip and Store package: that version and no other
- the architecture of each zip's binaries
- the version the x64 `winshell.exe` reports
- that every `winshell doctor` check runs from the x64 zip
- the manifests, against the validator the pinned `wingetcreate` uses, downloaded from NuGet into `artifacts/tools/`
- each architecture's identity, version and executables in the Store bundle

| Option | What it does |
| --- | --- |
| `-Publish`, `-Pack`, `-Verify` | Run one step. |
| `-Version` | Overrides the release from `.release-please-manifest.json`. |
| `-Tag v1.2.3` | Stops unless the checkout is that release. |
| `-Runtime` | Picks `win-x64`, `win-arm64` or both. |
| `-Out` | Chooses the folder. |
| `-IdentityName`, `-Publisher`, `-PublisherDisplayName` | Give the Store package the identity Partner Center reserved. Without them it has a test identity. |

### The install scripts

`pwsh scripts/install.ps1` installs the portable build for the current user from a GitHub release into `%LOCALAPPDATA%\Programs\winshell\bin`, adds that folder to the user PATH, and creates a Start menu shortcut. See the [README](https://github.com/enpaze/winshell/blob/main/README.md#install-with-powershell).

- `-Version x.y.z` picks a release, and `-AutoStart` registers sign-in. Under `irm | iex` those are `WINSHELL_VERSION` and `WINSHELL_AUTOSTART`.
- A leftover `bin.old` folder is removed on a later install once nothing from that install is running.
- If a Microsoft Store copy is also installed, it says so and still finishes, because that copy's WindowsApps alias usually comes first on PATH.
- `irm | iex`, and running the file with the call operator, also add that folder to the current window's PATH and tell you to run `winshell`. `pwsh scripts/install.ps1` starts a process that exits, so it tells you to open a new terminal.

`pwsh scripts/uninstall.ps1` removes that install folder, that PATH entry, that shortcut and that sign-in entry. It leaves `%APPDATA%\winshell`, and logs and state stay in `%LOCALAPPDATA%\winshell`. Under `irm | iex` it also removes that folder from the current window's PATH and leaves every other entry.

The Package workflow uploads both scripts next to the zip.

## CI

### Required checks

The single required test check, `ci`, collects suite, installer compatibility and quality results. The ruleset also requires `actionlint`, `pr-title` and `pr-rules`.

- `ci` fails if quality, installer compatibility or any shard fails or is cancelled; intentionally skipped jobs are allowed.
- `ci` runs lychee offline over the repo's Markdown, so a link to a file or a heading that isn't there fails the required check even when no build is needed.
- `pr-title` checks the pull request title.
- `pr-rules` checks that each type of change carries its proof, as "PR description" in `CONTRIBUTING.md` lists.
- The Package workflow runs the `-Tier Package` deployment validation on PRs that `scripts/ci-changes.ps1` flags `package`; it is not required. Leave `scripts/package.ps1` and arm64 to CI.
- CodeQL uses GitHub's separately managed default setup; its security-analysis triggers are independent of the CI workflow's file selection.

### How checks are selected

The `changes` job selects checks for PRs and pushes to `main`:

- A draft skips quality checks and suite shards; marking it ready runs them.
- `quality` always checks the links of every Markdown file. Prose-only changes stop there.
- Compiled documentation changes run only `DocsTests`.
- Other changes run file-only formatting and PowerShell suites alongside five .NET suite shards, which build their own projects.
- Non-Markdown files under `docs/samples/` count as code. Documentation-only changes skip the shards.
- A newer push cancels the superseded CI run on the same branch, including `main` and manual runs.
- `gh workflow run ci.yml --ref <branch>` runs every check by hand.

### Jobs

`check.ps1` requires Pester 5.6.1 and runs the PowerShell 7 suites. CI installs hash-verified modules through `.github/actions/powershell`; the installer job additionally runs the combined installer/uninstaller tests on Windows PowerShell 5.1 and feeds the required `ci` result. Workflow and link checks remain separate:

| Job | When it runs | How to run it locally |
| --- | --- | --- |
| `installer` | PRs and pushes to `main` that change `scripts/`, `tests/Scripts/` or workflows/actions. | Windows PowerShell 5.1 runs `tests/Scripts/Install.Tests.ps1` with Pester 5.6.1 against temporary profiles and releases served on loopback. PowerShell 7 tooling suites run once in `quality`. |
| `tests (1/5)` through `tests (5/5)` | Non-draft PRs and pushes to `main` that change more than docs or Markdown, in `ci.yml`. Results feed the one required `ci` check. | `pwsh scripts/check.ps1` for the tests that open no windows, and [A windowed test](#a-windowed-test) for the rest. See [Test shards](#test-shards). |
| `close-fixed-issues` | Every merged pull request, in `close-fixed-issues.yml`. Not required. | Nothing to run. Once the merge commit is on `main`, it closes each issue the pull request fixes that GitHub left open, with a comment naming the pull request. |
| `pr-size` | Every pull request, from `pr-size.yml` on the base branch. Not required. | Nothing to run. It labels the pull request `size:XS` to `size:XL`, as "PR description" in `CONTRIBUTING.md` says. |
| `trim-caches` | Every push to `main`, after `ci`, in `ci.yml` | Nothing to run. It deletes all but the two newest CodeQL databases of each language from the Actions cache, which would otherwise pass the 10 GB GitHub keeps. |
| `links` | Pushes to `main` that change docs or Markdown, and every Monday, in `links.yml` | lychee 0.24.2 over `docs/**/*.md`, `README.md`, `CONTRIBUTING.md`, `GLOSSARY.md` and `.github/*.md`, excluding `docs/agents`. |
| `actionlint` | PRs and pushes to `main` that change workflows or actions, in `ci.yml` | actionlint on `.github/workflows`, then zizmor on `.github`. |
| `package` | PRs that `scripts/ci-changes.ps1` flags `package`, pushes to `main` that change more than docs or Markdown, and releases, in `package.yml`. Not required. | `pwsh scripts/package.ps1`. It publishes both architectures and needs the Windows SDK for the MSIX check. The workflow's `publish` job builds each architecture on its own runner, `windows-2025` and `windows-11-arm`, and `package` packs both. The `arm64` job then runs that zip's `winshell.exe --version` on `windows-11-arm`. |

### Test shards

CI calls `Get-TestRun -Shard <1..5>` and `Invoke-TestRun -Shard <1..5>` from `scripts/lib/Tests.ps1`:

- Together they run every test project, the CLI suite and the category `Window` included.
- CLI, Shell and Services use the first three workers. Other test projects share the fourth, and CLI widget tests use the fifth.
- New projects join the fourth, and new CLI classes join the first.
- Shard 2 also compiles the template against source libraries, the solution's projects under `examples/` and `docs/samples/`, and the reference generator as a program, sharing the Shell libraries it already builds.
- Each worker prints its ten slowest tests.
- `Invoke-TestRun -ResultsDirectory <folder>` retains results in a unique child folder; CI uploads them as `test-results-<shard>` for seven days, including failed runs. Without that argument, local runs clean up their results.

### Runners

- CI uses free runners only.
- Jobs that build keep NuGet packages on the runner's work drive, where they restore several times faster than on the system drive.
- The `windows-2025` workers run Windows Server 2025, build 26100, not Windows 11, so a native test that passes on your PC can fail there. One known difference: Windows Graphics Capture refuses to capture a tool window by itself there (`CreateForWindow` fails with `E_INVALIDARG`), and Windows 11 build 26200 captures one.

### Merging

`main` takes squash merges of pull requests. `gh pr merge <number> --squash --auto` merges one once its required checks pass. A pull request need not be up to date with `main`, so its checks ran on its branch and not on what lands; the push to `main` selects checks again for the merged change.

The short `pr-title` and `pr-rules` workflows finish every run, including successive PR edits. They have no concurrency cancellation: a cancelled required check can block a pull request even after a newer passing run. The main CI workflow cancels superseded runs on each branch. To recover an already cancelled metadata check, open its Actions run and choose **Re-run all jobs**, or run `gh run rerun <run-id>`. Only **Merged** confirms the change is on `main`.

## Docs

The docs are plain Markdown, read on GitHub or in an editor. `docs/agents/` is for agents working on this repo.

- **A new reference page** goes into the list in `docs/reference/index.md`.
- **Links between pages** are relative `.md` links, also to files outside `docs/`, such as `../CONTRIBUTING.md`. The `ci` job runs lychee offline over every Markdown file, with `--include-fragments`, and fails on a link to a file or a heading that isn't there.
- **C# examples** in a `csharp` code block are checked by `DocsTests`: each one is text from `tests/Winshell.Shell.Tests/GuideSamples.cs`, which the test project builds.
- **The reference** in `docs/reference/` has a page per namespace of the `Winshell` package (the public types of `Winshell.Shell`, `Winshell.Services` and `Winshell.Core`), `winshell.md` for `Winshell` and `winshell-<namespace>.md` for the others, starting with `# <namespace>` and an intro written by hand. `dotnet run scripts/generate-reference.cs` writes the rest, a `## Type` section for each public type with its members, from the `///` comments, and `DocsTests` fails when a page is not what it writes.
- **Links to other sites** are checked by `links.yml`, on `main` and every Monday. That job isn't part of `check.ps1`.

## Environment variables

A running shell reads these. None is needed to build or test.

| Variable | What it does |
| --- | --- |
| `WINSHELL_CONFIG_DIR` | Folder named configs are loaded from, instead of `%APPDATA%\winshell`. |
| `WINSHELL_LOCAL_DIR` | Folder used instead of `%LOCALAPPDATA%\winshell` for cache, state, data, instance logs, crash reports and clones of installed widgets. A child process inherits it. |

### Installer

`scripts/install.ps1` and `scripts/uninstall.ps1` read these. A normal install sets none of them. `WINSHELL_VERSION` and `WINSHELL_AUTOSTART` are the `irm | iex` forms of `-Version` and `-AutoStart`; they are described with [the install scripts](#the-install-scripts).

| Variable | What it does |
| --- | --- |
| `WINSHELL_RELEASE_BASE` | Base URL `install.ps1` downloads from, instead of `https://github.com/enpaze/winshell/releases`. A trailing slash is ignored. Any other value must be `http://127.0.0.1` or `http://localhost`, with an optional port and path. Anything else stops the install. Installer tests point this at a local server. `uninstall.ps1` does not read it. |
| `WINSHELL_INSTALL_TEST` | `1` marks an installer test. Both scripts then refuse the real `%LOCALAPPDATA%` and `%APPDATA%`, and they require `WINSHELL_USER_PATH_FILE` and `WINSHELL_AUTOSTART_FILE`. `install.ps1` then does not ask Windows for a Microsoft Store copy. Any other value, including unset, is a normal install. |
| `WINSHELL_USER_PATH_FILE` | File used instead of the user `Path` value under `HKCU\Environment`. The scripts read and write that file as UTF-8 without a BOM. A missing file reads as an empty path. While it is set, the scripts leave the session's PATH alone. Set only from installer tests. |
| `WINSHELL_AUTOSTART_FILE` | File used instead of the `Winshell` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. `install.ps1 -AutoStart` writes the sign-in command there as UTF-8 without a BOM, and `uninstall.ps1` deletes the file when that command starts this install. A missing file means sign-in is off. Set only from installer tests. |

## Where files live

| What | Where |
| --- | --- |
| User configurations | `%APPDATA%\winshell`, or `WINSHELL_CONFIG_DIR` when it is set |
| Cache | `%LOCALAPPDATA%\winshell\cache\by-shell\<shell id>` |
| State | `%LOCALAPPDATA%\winshell\state\by-shell\<shell id>` |
| Per-shell data | `%LOCALAPPDATA%\winshell\data\by-shell\<shell id>` |
| Instance logs | `%LOCALAPPDATA%\winshell\run\<id>\log.txt` |
| Crash reports | `%LOCALAPPDATA%\winshell\crashes`, one `crash-<UTC time>Z-<process>-<where>.txt` each. See [Crash reports](#crash-reports). |
| Run at sign-in | The `Winshell` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, or the package's startup task. See [The sign-in entry](#the-sign-in-entry). |

### Crash reports

Nothing is sent anywhere. What writes a report:

- **A window** whose native window cannot be made or placed, or throws while drawing or handling input, writes one, closes, and opens again after 1, 2, 4, 8 and 16 seconds. A sixth failure within five minutes leaves it closed until the next reload.
- **A frame** the graphics device cannot draw is skipped and reported once per window.
- **Work on the event loop** that throws, or a background task that fails, writes one per exception and stack.
- **A crash that ends the process** writes one before the log closes.

What a report holds, and how long it stays:

- A report holds the last 40 log lines. It takes out the passwords, tokens, keys and URL queries Winshell recognizes.
- The newest 50 reports are kept by their UTC time. Only files named and headed as reports are deleted, and nothing is deleted when the folder is a link or junction.

### The sign-in entry

**An unpackaged Winshell** writes the `Winshell` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`:

- when `install.ps1 -AutoStart` runs, which starts `winshell.exe` with no configuration, or
- when a shell sets `StartupService.RunAtSignIn`, which starts this install's `winshell.exe run -p` of that configuration.

Either form runs under `conhost.exe --headless` with `--no-build` and `--no-duplicate`, so no console window opens and sign-in does not build. A failure before the shell starts shows a dialog.

- The installer's command counts as this configuration only while a bare `winshell run` would start it, and that is read again each time the entry is read.
- Environment variables are not copied: `WINSHELL_CONFIG_DIR` applies at sign-in only when it is set for the user.
- Windows' Startup apps page can turn the entry off; Winshell reads that and never changes it.
- `winshell.exe` and `winshellw.exe` in the same folder are one install: an entry for either reads as this Winshell.
- An entry that starts a Winshell in another folder, or another configuration, reads as `RegisteredElsewhere`, and setting `RunAtSignIn` rewrites it for this one.

**A packaged Winshell** does not write that value. It enables the package's `windows.startupTask` (task id `Winshell`, declared off) which starts `winshellw.exe --no-build --no-duplicate`. Windows owns that launch, so a Store update does not leave a path, and `RegisteredElsewhere` stays false.

## Windows access, and what tests may touch

### Network permissions

- Reading connection profiles needs no setup.
- Wi-Fi scanning/connecting requires Windows Wi-Fi/location access and an explicit `NetworkingService.RequestAccess()` call; packaged deployments also need `wiFiControl`.
- Setting `WifiEnabled` is a separate radio-control request, and a packaged app declares the `radios` device capability.
- **In tests:** automated tests pass fake radios and fake radio access, and never call `Radio.SetStateAsync` or `Radio.RequestAccessAsync`. Native smoke tests only read state.

See [networking](reference/winshell-network.md).

### Bluetooth permissions

- Reading Bluetooth state uses Windows adapter and device-enumeration properties, plus `BluetoothIsDiscoverable` and `BluetoothIsConnectable`, and does not request control of radios or open remote device handles. Packaged deployments need the `bluetooth` capability.
- Setting `BluetoothAdapter.Enabled` requests radio control, and a packaged app also needs the `radios` capability.
- Setting `Discoverable` or `Pairable` calls `BluetoothEnableDiscovery` or `BluetoothEnableIncomingConnections`.
- Explicit pairing relies on Windows' pairing UI and deployment/policy support; errors are reported on the service.
- **In tests:** automated native validation only reads. Scanning, device actions and these visibility changes use a fake backend and never call `BluetoothEnableDiscovery` or `BluetoothEnableIncomingConnections`. Radio changes use fake radios and never call `Radio.SetStateAsync`.

See [Bluetooth](reference/winshell-bluetooth.md).

### Capture validation

`Winshell.Capture` uses Windows.Graphics.Capture desktop interop with the system border enabled. No additional package or borderless capability is required. Windows policy, protected sources, session state and device availability can restrict capture. See [capture](reference/winshell-capture.md) for the API and limits.

### Run at sign-in

The startup tests, such as `StartupRegistryTests`, use a fake, or a key of their own under `HKCU\Software\Winshell\Tests` that they delete. They never write the user's `Run` or `StartupApproved` keys. The startup task is a fake as well: the tests never call `StartupTask.GetAsync`, and they do not install a package.

### Hotkeys

Tests may register an unused chord such as Ctrl+Alt+Shift+F24 and may install a keyboard hook while a Windows-key hotkey is enabled or a key sequence is in progress. Each test removes the registration and the hook before it ends. Tests do not inject keystrokes of their own or change keyboard settings. The one exception is `FocusTakeoverTests`, a [windowed test](#a-windowed-test): it sends one key tap, and sets the session's foreground lock timeout and puts it back. A test may run `winshell inspect`'s real input commands, which keep to the rules under [See what you changed](#see-what-you-changed). See [hotkeys](reference/winshell-input.md).

### Checks only a live session can do

Real mixed-DPI movement, Acrylic composition, Narrator speech, native toast placement, and listener permission with a deployed package still need an interactive Windows session.

## Releasing

### From a merge to a published release

release-please keeps a release PR open that collects the changes merged into `main`, with the next version and its changelog. Merging that PR tags the release and creates a **draft**. `draft` and `force-tag-creation` in `release-please-config.json` keep the release unpublished while making its tag available for packaging.

The release workflow then runs the Package workflow (`.github/workflows/package.yml`) on the tagged commit:

1. It runs `scripts/package.ps1`.
2. `scripts/publish-release.ps1` attaches both portable zips, `SHA256SUMS.txt`, `winget-manifests.zip`, `install.ps1` and `uninstall.ps1` to the draft. Every file must exist locally, and every remote asset must have the expected SHA-256 before the draft is published. An existing asset is never replaced: matching bytes are skipped and a different digest stops the upload.
3. Signing notes are also added before publication.

This order supports [GitHub immutable releases](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases), whose assets are frozen when published.

The same workflow builds and checks the packages, without publishing, on the PRs and pushes to `main` the [`package` job](#jobs) runs for, and keeps them as the run's `packages` artifact. The Store bundle from that run is kept for 14 days.

### The version

The released version is `.release-please-manifest.json`. A build stamps that into the assembly unless `-p:Version=` overrides it, so `winshell --version` and `Shell.Version` report that release. The printed version can also carry a `+` build id. A build that cannot read the manifest is `0.0.0-dev`.

### winget

`winget install DanteSparras.Winshell` is not available yet: the first submission is not done.

With a `WINGET_TOKEN` secret, the release workflow sets `WINGET_CREATE_GITHUB_TOKEN` for a pinned `wingetcreate` and submits the manifests to [winget-pkgs](https://github.com/microsoft/winget-pkgs). That secret is not set.

- Before it is added, it has to be a token from a dedicated bot account whose only repository is that account's fork of winget-pkgs. A token from the maintainer account is not used: a classic `public_repo` personal access token can write every public repository that account can access, including this one.
- Without the secret, submit the manifests by hand: unzip `winget-manifests.zip`, set `WINGET_CREATE_GITHUB_TOKEN`, and run `wingetcreate submit <folder>`.
- winget's moderators review every PR, and the first one for `DanteSparras.Winshell` before any other.

### Signing

A release's own binaries are signed through [SignPath Foundation](https://signpath.org) once its project is set up. That project is not set up, so published zips are unsigned.

Once it is, the Package workflow uploads the published folders as the `unsigned` artifact, asks SignPath to sign them with the `parameters` version the binaries carry, waits up to 110 minutes for an approver, and packs and checks the signed files (`scripts/package.ps1 -Verify -Signed`). It needs:

- the repository variable `SIGNPATH_ORGANIZATION_ID`, which turns signing on. Without it, releases are not signed.
- the secret `SIGNPATH_API_TOKEN`, from a SignPath CI user that may submit to the project.
- `SIGNPATH_PROJECT_SLUG` and `SIGNPATH_SIGNING_POLICY_SLUG` when they are not `winshell` and `release-signing`.
- the signing policy's allowed refs to include `refs/heads/main` and `refs/tags/v*`, since a repair runs from the tag.
- the project's artifact configuration, [packaging/signpath/artifact-configuration.xml](https://github.com/enpaze/winshell/blob/main/packaging/signpath/artifact-configuration.xml), pasted into SignPath, and GitHub set up as its trusted build system.

PRs and pushes to `main` are never signed. Every binary already carries the product name `Winshell` and the release's version, which `-Verify` checks with or without signing.

### A draft with missing files

- **Packaging or the upload failed:** open the Release Please run and choose **Re-run failed jobs**. The release stays a draft until every asset verifies.
- **Packaging never ran**, because the whole Release Please run was re-run after the draft existed: run Package by hand (**Actions > Package > Run workflow**). Under **Use workflow from**, choose **Tags** and the draft's tag, and give the same tag. The tagged commit must contain this draft-publication workflow and its scripts. A signed release must be built from the commit the run is for, so starting its repair from `main` stops. The workflow checks the manifest version, resumes matching draft assets and publishes only after verification.

A **published** release with missing files is not repaired by this workflow. In particular, v0.6.0 was published immutable before its assets were uploaded ([#166](https://github.com/enpaze/winshell/issues/166)). Rerunning its workflow cannot add those assets, and disabling repository immutability does not unlock that release. Leave its tag and assets alone. Recovery requires a later approved release, created as a draft, packaged, populated and verified before publication.

### Microsoft Store

The Store package is not published. Publishing waits on Partner Center setup: the identity variables below. Until then it is not an install option.

The Package workflow also builds `winshell-<version>.msixbundle` (x64 and arm64) from `packaging/msix/AppxManifest.xml` and keeps it as the run's `store-package` artifact. It is not attached to the release: it is unsigned, and the Store signs it. Each run also test-signs the x64 package with a certificate only that runner trusts, installs it there and runs the `winshell` alias.

About the Store build:

- **Files.** It reads and writes the same `%APPDATA%\winshell` config folder as every other build. The manifest turns off file and registry write virtualization (`unvirtualizedResources`), so those writes are the real ones.
- **Sign-in** is the package's startup task, declared off, not the user's `Run` key: Windows starts `winshellw.exe --no-build --no-duplicate`, which keeps working after a Store update.
- **Capabilities.** The package declares `userNotificationListener`, `wiFiControl`, `bluetooth`, `location` and `radios`.
- **Start** runs `winshellw.exe --no-duplicate`, so a second click starts no second shell. `winshellw.exe` is a copy of `winshell.exe` that opens no console, made when the build is published, and the portable zips have it too. `winshell` in a terminal is the command line.
- **Version.** The Store takes no 0 as the first part of a package version and installs the highest, so Winshell x.y.z is package version (x+1).y.z.0.
- **Identity.** The repository variables `MSIX_IDENTITY_NAME`, `MSIX_PUBLISHER` and `MSIX_PUBLISHER_DISPLAY_NAME` are the identity Partner Center shows under **Product identity**. Without them, the package is `Winshell.Test` by `CN=Winshell Test`, which the Store refuses.

To submit a release:

1. Download `store-package` from the release's Package run and upload the `.msixbundle` in Partner Center.
2. Paste the listing from `packaging/store/listing.md`.

### Other release tasks

- **Pick the version yourself:** when merging a PR, add `Release-As: x.y.z` to the end of the squash commit message.
- **The release PR's checks don't start:** without the `RELEASE_PLEASE_TOKEN` secret, close and reopen the release PR right before merging it.

## Project structure

```text
.
├── .github/                         # CI, issue and PR templates, Dependabot
├── docs/
│   ├── adr/                         # Architecture decision records
│   ├── agents/                      # How agents work on this repo
│   ├── reference/                   # A page per namespace
│   ├── research/                    # Research notes that a tracked decision builds on
│   ├── samples/message-widget/      # The message widget the configuration guide builds; not a shell
│   ├── *.md                         # The configuration guide and troubleshooting
│   └── development.md               # Setup, environment, and every command
├── examples/
│   └── omarchy-style/               # A configuration in the style of Omarchy's top bar; the solution builds it
├── packaging/
│   ├── logo.svg                     # The app logo; scripts/render-logo.cs renders its PNGs and winshell.ico
│   ├── msix/                        # The Microsoft Store package's manifest and assets
│   ├── signpath/                    # The artifact configuration SignPath signs releases with
│   └── store/                       # The Store listing text and its app tile
├── scripts/                         # Check, test, pack, package, install and release scripts; docs/development.md lists them
├── templates/                       # dotnet new winshell
├── src/
│   ├── BannedSymbols.txt            # Clock calls that don't build; time comes through a TimeProvider
│   ├── Winshell/                    # winshell.exe: the CLI, which builds and supervises a configuration
│   ├── Winshell.Native/             # Win32/COM declarations, unsafe interop and the safe operations over them
│   ├── Winshell.Core/               # The event loop contract, the log, ObservableObject, TOML files, crash reports, and the file helpers they use
│   ├── Winshell.Services/           # Windows services, instances, and the control pipe
│   ├── Winshell.Shell/              # Shell.Run and WinUI windows
│   └── Winshell.Package/            # Packs Native, Core, Services and Shell as the single Winshell package
├── tests/
│   ├── Winshell.Architecture.Tests/ # The layer and feature backend boundaries, and no hand-written unsafe code in WinUI projects
│   ├── Winshell.Core.Tests/         # The log, TOML files, crash reports and file helpers
│   ├── Winshell.Services.Tests/     # The services; only the category Window opens a window
│   ├── Winshell.Shell.Tests/        # A real shell and the event-loop contract; only the category Window opens a window
│   ├── Winshell.Tests/              # Runs winshell.exe instances side by side
│   ├── Winshell.Package.Tests/      # Packs the template and runs its shell; Category=Package, left out of the main run
│   ├── Scripts/                     # PowerShell tooling tests and their fixtures
│   ├── TestSupport/                 # Source files every test project links instead of copying
│   └── Directory.Build.props        # What every test project shares
├── winshell.slnx                    # The solution the check builds
├── Directory.Build.targets          # Seals interop into Winshell.Native: bans interop attributes elsewhere, and unsafe code outside WinUI projects
├── BannedSymbols.Interop.txt        # The interop APIs no project but Winshell.Native may use
├── CONTRIBUTING.md                  # Test rules, branch names, PR titles and descriptions
└── GLOSSARY.md                      # The words this repo uses
```
