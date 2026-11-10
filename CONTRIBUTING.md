# Contributing

## Where the standards live

A change is held to these, by people and by the `code-review` skill:

- [How we build](AGENTS.md#how-we-build): the owner's notes on simple systems and scope.
- [The architecture](docs/architecture.md): the layers, their boundaries and the sealed interop.
- [Formatting and style](docs/development.md#formatting-and-style): C# whitespace, code style, names and XAML.
- The test rules: [Where a test belongs](#where-a-test-belongs), [Write tests that don't flake](#write-tests-that-dont-flake), and the installed `test-audit` skill.
- [The glossary](GLOSSARY.md): the words to name things with.
- [The ADRs](docs/adr/): the decisions already made.

## Run the checks

The [development runbook](docs/development.md) covers the prerequisites, the first checkout, and every command. Before every push, run the [checks for the change](docs/development.md#checks-by-change): prose-only changes need lint; compiled documentation needs its targeted tests; other changes need the full check. The full check opens no windows: it leaves out every test in the category `Window`, which CI runs on every PR.

## Where a test belongs

Every new or changed test passes the authoring gate of the `test-audit` skill, which `pwsh scripts/install-skills.ps1` installs (see [Agent skills](docs/development.md#agent-skills)). In this repo, each contract has one owner, at the cheapest boundary that can show it:

| Project | Owns |
| --- | --- |
| `Winshell.Architecture.Tests` | The boundaries of [the architecture](docs/architecture.md#architecture-proof): layers, feature backends, and no hand-written unsafe code in WinUI projects |
| `Winshell.Core.Tests` | The shared infrastructure: the log, TOML files, crash reports and file helpers |
| `Winshell.Services.Tests` | A service against a fake of its Windows backend, on the test event loop |
| `Winshell.Shell.Tests` | A real shell with real windows, and the event-loop contract on the test loop and `DispatcherLoop` |
| `Winshell.Tests` | `winshell.exe` and shell programs as separate processes |
| `Winshell.Package.Tests` | The packed library and template, `Category=Package` |

- **A windowed test** shows a window on the desktop, takes the foreground, sends real input, or hides Explorer's taskbar. It carries `[Trait("Category", "Window")]`, so the full check leaves it out. See [A windowed test](docs/development.md#a-windowed-test).
- **A regression test goes red before the fix** for the intended reason, and green after it. Say so in the PR, or say why you couldn't show it.
- **A changed or moved test keeps every assertion it had, and a change that keeps behavior keeps its tests**, or the PR names the cheaper test that proves what was dropped, and the reviewer opens that test.
- **One bug gets one test**, at its owner, however many layers it crossed.
- **No seams for tests alone.** A test needs no `internal` member, constructor overload, flag or hook that production code doesn't use.
- **Keep these tests even when they are static or slow:** the public API of the `Winshell` package, the control pipe protocol, CLI output and exit codes, TOML and other files on disk, security guards, platform behavior, defaults, generated templates, the package, the release scripts, and observable ordering.

## Write tests that don't flake

A test waits on a real signal: an event, a task's completion, or a fake clock.

- **No sleeps or fixed timeouts as a way to wait.** A timeout can stop a test that hung, but a passing test must never depend on how long something took. `Thread.Sleep` and `Task.Delay` don't build in `tests/`. When nothing gives a signal, such as a file or another process, use `Wait.Until` from `tests/TestSupport/Wait.cs`.
- **Only `Wait.ForNothingToHappen` waits for a set time.** Use it when a check is that something did not happen. A slow machine can then miss a bug, but it can't fail a test that should pass.
- **Pass `TestContext.Current.CancellationToken` to anything that waits.** It is cancelled when a test has run for two minutes, so the test fails alone and the run goes on.
- **Time comes through a `TimeProvider`.** `DateTime.Now`, `Stopwatch` and their relatives don't build in `src/`. A test passes a `FakeTimeProvider`, given a UTC time. `TimeProvider.System` is for what only real time can drive: a file's write time, a Windows object, the CLI's own process.
- **Use the cheapest boundary that proves the contract.** Service policy uses fake backends and fake time; native rendering, process lifecycle and transport contracts use real Windows objects. Reuse hosts and processes, and keep all of them in the same suite. `Wait.Until` is for what really gives no signal: another process, or a window.
- **Don't depend on a free thread pool worker.** A busy pool can hold back queued work for seconds, so anything that has to happen on time needs its own thread.

### A test that fails sometimes is a bug

Nothing retries a test and nothing sets one aside. A test that fails sometimes is a bug in the test or in Winshell, so find the cause and fix it, on `main` as much as in a PR. Tests are built so that they can't flake:

- **Each test owns what it uses.** It has its own temp folder and leaves no global state behind. A test that needs something one at a time, such as the keyboard hook, joins an xUnit collection with the others that do.
- **Order and timing are shaken on every run.** Classes run in parallel on twice as many threads as cores, and the order changes each time. Test assemblies run in sequence locally; CI spreads the suite across five separate Windows workers. The run prints its seed, and `pwsh scripts/check.ps1 -Seed <seed>` plays each assembly's order back; for a windowed test, end its `dotnet test` command with `-- xUnit.Seed=<seed>`.
- **A time limit only catches a hang.** A test claims nothing about how fast something is on this machine today, so set a limit far above what the work takes under load: only the defect it guards can miss it.

## Open a PR

### Branch name

`<type>/<short-description>`, lowercase, using a type from the table below. For example, `fix/duplicate-orders` or `feat/csv-export`.

An AI agent names its branch `agent/<tool>/<issue#>-<slug>` instead, after its ticket, such as `agent/claude/123-tray-overflow`.

### PR title

A [Conventional Commit](https://www.conventionalcommits.org) in plain language: `<type>(<optional scope>): <what changed>`, starting lowercase. For example, `fix(api): retries no longer duplicate orders`.

| Type | Use it for |
| --- | --- |
| `feat` | A new feature |
| `fix` | A bug fix |
| `docs` | Documentation only |
| `style` | Formatting, with no change in behavior |
| `refactor` | Restructuring code, with no change in behavior |
| `perf` | A performance improvement |
| `test` | Adding or fixing tests |
| `build` | The build system or dependencies |
| `ci` | CI configuration |
| `chore` | Maintenance that fits nowhere else |
| `revert` | Undoing an earlier change |

For a breaking change, add `!` before the colon: `feat(api)!: remove the v1 endpoints`.

Commits on your branch can use any message; the squash merge replaces them with the PR title. Using the same format for them is still a good habit.

CI checks the PR title. It also checks the branch name, but only as a reminder; it doesn't block merging.

### PR description

Fill in the PR template. An AI agent writes the body with the installed `pr` skill; the template uses the skill's sections and adds only the lines this repo needs. Skip preambles, keep prose brief, and use the words of [GLOSSARY.md](GLOSSARY.md).

- **Summary**: the smallest diagram, diff sketch or tree that makes the change clear, next to the short text it supports. Then `Fixes #<issue>`, or why the change needs no issue.
- **Evidence**: concrete evidence that the change works, as a before and after: a screenshot for a visual change, otherwise the exact test or output that failed and now passes. "Tests pass" is not evidence. Add **Not verified:** for anything you couldn't check. Don't commit evidence to `main`.
- **Proof for the type of change**: the `pr-rules` check reads these from your title, body and files.
  - `fix` adds a test that failed before the fix, and says how under **Before:** and **After:**. If it can't have a test, say why on a `**No test:**` line. A fix to docs alone doesn't need one.
  - `perf` gives a number under **Before:** and under **After:**, or in a table with those headings.
  - A `feat`, `fix` or `perf` change to XAML in `src/` shows a `winshell inspect` screenshot. `pwsh scripts/pr-screenshot.ps1 <png>` publishes one and prints the Markdown to paste. If nothing visible changed, say why on a `**Not visual:**` line.
  - Other types carry no extra proof.
- **Merge Danger**: whether this is a one-way or two-way **Door**, and its **Blast Radius**, the one word for what it can affect.
- **Agent**: if an AI agent wrote the PR, the model and the harness, such as "Claude Opus 5.5 in Claude Code".

The `pr-size` job labels each pull request `size:XS` to `size:XL` by its changed lines: XS under 10, S under 30, M under 100, L under 500, and XL 500 or more. Lines in `tests/` count only when nothing else changed.

## Conflicts and merging

`main` has no merge queue and doesn't require a PR to be up to date, so two PRs that each pass can break `main` together.

### Shared files

One PR at a time changes a shared file. `gh pr list --json number,headRefName,files` shows what open PRs touch. The shared files are:

- a `Directory.*` build file, such as `Directory.Packages.props` or `Directory.Build.targets`
- `global.json`, `nuget.config`, `.editorconfig` and `winshell.slnx`
- `src/BannedSymbols.txt` and `BannedSymbols.Interop.txt`
- `tests/TestSupport/`
- a lockfile
- a type more than one module uses

### When your PR conflicts with `main`

1. **Rebase** onto the latest `origin/main`; a stacked PR runs `gh stack rebase`. Never merge `main` into your branch. Force-push only your own PR branch, with `git push --force-with-lease origin HEAD`, never `main`.
2. **Resolve each conflict by intent.** Find the PR that changed the same code with `git log --oneline <your-branch>..origin/main -- <file>`, read it, and keep both behaviors. Taking "ours" or "theirs" wholesale is never a resolution.
3. **Escalate a collision.** If both sides change the same behavior, run `git rebase --abort`, comment on your PR naming the other PR and the collision, and escalate to the lead thread, or to the user if there is none, instead of guessing.
4. **Check again.** Run the [checks for the change](docs/development.md#checks-by-change) before you push, even after a clean rebase: it can still break logic.

### Until it merges

You own the PR until it merges:

- Fix every failing check, and re-run a cancelled one with `gh run rerun <run-id>`.
- For a standalone PR, enable auto-merge with `gh pr merge <number> --squash --auto`. A delegated one goes to its lead, as [the lead page](docs/agents/lead.md) describes.
- Confirm `gh pr view <number> --json state --jq .state` prints `MERGED`.
- Whoever sees it to `MERGED` then cleans up the ticket's worktree:
  1. A worktree you made with `git worktree add` is yours: remove it with `git worktree remove <folder>`. A folder a tool still holds open is that tool's to remove, and git keeps its branch until it does.
  2. Run `git worktree prune`, then delete the local branch with `git branch -D <branch>`.
- Keep `release-please--branches--main`: it is the release bot's branch, and deleting it closes the open release PR.
