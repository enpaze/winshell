# Winshell and Winland are siblings

Winshell and Winland stay two products in two repos. A sibling is built, tested, documented, and released the same way, and it does a different job. They share no package, no submodule, and no third repo. The named pipe stays Winland's protocol, and Winshell keeps its own client for it.

Both use Winshell's modular monolith. The projects sit in one order: Native, then Core, then one assembly of feature folders, then the exe that composes them. The exe is not a layer. Native owns CsWin32 and every Windows interop. Unsafe code compiles only there. `Directory.Build.targets` and an architecture test hold that line.

Core is shared infrastructure of the same kind in both. It reads TOML with Tomlyn, and it holds the log and the file and version helpers. It holds no shell feature and no window-manager rule. Winland's window manager lives in `Winland.Services`, in feature folders. A feature owns its backend. A backend is a platform operation that feature needs, and no interface is added only so a test can substitute one. The pure window-manager logic stays testable without a desktop.

`Winshell.Shell` is Winshell's layer above Services: the WinUI library and the author API. Winland has no Shell project. `Winland.Daemon` is Winland's exe. `Winland.Install` and `Winland.Launcher` stay outside the layers, as `Winshell.Package` does. Winland's four-layer clean architecture is superseded by this shape, not kept beside it. Winshell does not grow a Domain project or an Application project.

The contributor toolbox is Winshell's, copied into each repo. `scripts/check.ps1` is the one gate: it builds, formats, and runs the suite. Tests are xunit v3. Each production project that has behavior of its own has one test project: Core, Services, the exe, Install, and Launcher. Native has none. An architecture test project covers Native and the dependency order. Winland's end-to-end tests live in `Winland.Daemon.Tests` and run in that suite. Each one starts an isolated Winland, with its own `WINLAND_PIPE`, its own `WINLAND_STATE`, and a config that ignores every window except the test's. They run while another Winland is running. They fail, and name the reason, only when an isolated instance cannot run beside another one. They never skip. The smoke project, `smoke.ps1`, and `smoke.yml` go away. Link and workflow lint are lychee, actionlint, and zizmor, through `check.ps1 -Lint` and the links workflow. Winland's hand-written markdown link checker goes away. The solution file is `<product>.slnx` in lowercase. `global.json` rolls forward by patch. `nuget.config` clears inherited sources. Shared tools use the same package versions, with transitive pinning.

A version is cut, tagged, and read into the build the same way in both. release-please keeps the version in `.release-please-manifest.json`. `Directory.Build.props` stamps that version into the build unless `-p:Version=` overrides it, and a build that cannot read the manifest is `0.0.0-dev`. What a user installs stays the product's own: the MSIX, the Store build, SignPath, and the template package for Winshell; a published GitHub release, a scheduled task, and an elevated process for Winland.

Both have `docs/adr/README.md`, a short `AGENTS.md`, and `docs/releasing.md` for the shared cut, tag, and version stamp. How a product is installed stays written for that product. Both run ci, release-please, the link check, and zizmor, and both protect release tags the same way. The package workflow stays Winshell's. The rolling `dev` prerelease and the stale-issues workflow stay Winland's.

Each product keeps its own glossary. Sibling and Layer are the terms with this same wording in both.

## Considered options

- **One repo, or a shared package.** A change in either product would then take a release of the shared part. The products do different jobs. Copying the shape is the smaller thing.
- **A third layout that averages the two.** Winland's clean architecture and Winshell's monolith mixed into something neither repo is. That is a new thing to learn.
- **The same installer.** An MSIX for Winland, so the release flows look alike. Winland runs elevated and installs from a scheduled task. Winshell is a packaged shell toolkit.
- **An ordinary suite plus a slow one.** A smoke script and a smoke workflow beside the tests. The end-to-end tests run in the one suite.
- **A hand-written link checker.** Winland's markdown walker. The tools are lychee, actionlint, and zizmor.

## Consequences

- Winland supersedes its own clean-architecture ADR when its projects move. This record does not replace that file.
- That move waits until `feat/fewer-options` ("Make the Product Obvious") has merged. It is one outcome, and it may land as more than one pull request.
- An end-to-end test opens only the windows its own config allows, so a check can run while a Winland is already running.
