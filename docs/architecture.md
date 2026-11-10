# Architecture

Winshell is a modular monolith: one program, with assembly boundaries for shared infrastructure and folder boundaries for service features. The build enforces the [sealed interop](#sealed-interop) boundary, and the [architecture tests](#architecture-proof) enforce the layers and feature backends.

## Dependency direction

The production libraries follow this order, from their consumers toward their dependencies:

```text
Winshell.Shell -> Winshell.Services -> Winshell.Core -> Winshell.Native
```

- A project may reference a lower layer directly when it needs that layer's contract. For example, Shell can call a safe Native window operation without adding a forwarding method to Services.
- A project must never reference a higher layer or form a cycle.
- Native references no other Winshell project.
- The CLI composes these libraries and owns process supervision. The package project assembles their distribution.

| Owner | Responsibility |
| --- | --- |
| `Winshell.Native` | Native declarations, COM contracts, unsafe interop, marshaling, native resource ownership and safe managed operations. |
| `Winshell.Core` | `IEventLoop`, `Log`, `ObservableObject` and `ObservableList` with the read tracking `Watch` follows, TOML and Crashes, with the file and version helpers they use. Shared infrastructure has no dependency on a service feature or WinUI. |
| `Winshell.Services` | One assembly of feature folders. Services apply policy and publish state on the event loop; feature backends perform platform work. |
| `Winshell.Shell` | WinUI hosting, the production event loop, window lifecycle and composition through the existing public Shell API. |
| CLI (`src/Winshell`) | Commands, configuration builds and shell process supervision. |

Keep the existing public Shell API. Moving an implementation does not justify compatibility shims, forwarding assemblies or duplicate implementations. Keep shared infrastructure in Core and feature policy in its owning Services folder.

## Sealed interop

Only `Winshell.Native` may contain `LibraryImport`, `DllImport`, `ComImport` or unsafe interop. This restriction applies to every other project, including tests. Other assemblies consume managed contracts that do not require pointers, unmanaged function pointers or unsafe callers.

### Inside Native

- **Declarations.** CsWin32 generates the Win32 declarations from an explicit API list, `NativeMethods.txt`. Add a Win32 API to that list, and remove the handwritten declarations a generated one replaces. Native may own declarations CsWin32 cannot supply, such as undocumented interfaces. Do not maintain duplicate Win32 declarations alongside generated ones.
- **Ownership.** Keep allocation and release together: handles, COM objects, buffers and callbacks have an explicit owner and lifetime. Callback exceptions must not escape into native code.
- **The boundary.** Put safe operations at this boundary. Do not expose the generated declaration set as a public service API or move feature policy into Native.

### How the build enforces it

The root `Directory.Build.targets` enforces it for every project except Native:

- It adds `BannedSymbols.Interop.txt`, so `DllImport`, `LibraryImport`, `ComImport`, `GeneratedComInterface` and `GeneratedComClass` fail with RS0030. So do `NativeLibrary` and `Marshal.GetDelegateForFunctionPointer`, which together would call native code with neither an attribute nor unsafe code.
- Outside WinUI projects it forces `AllowUnsafeBlocks` off, so unsafe code fails with CS0227. It runs after each project file, so setting `AllowUnsafeBlocks` there has no effect.
- A new project under `src/` or `tests/` is covered without edits.

Native keeps the clock ban the other `src/` projects have. Do not exempt a consumer, suppress the diagnostics or exclude generated consumer code to bypass the boundary.

### WinUI projects

A project with `UseWinUI` keeps unsafe compilation, because CsWinRT generates its trim-safe vtables and generic instantiations only when unsafe code is allowed. Hand-written unsafe code there is still forbidden; the [architecture tests](#architecture-proof) check for it. The interop ban applies to WinUI projects too.

The one exception is the Windows App SDK's `UndockedRegFreeWinRT-AutoInitializer.cs`, which its package adds to each self-contained WinUI executable. It is package startup code we don't own, not our interop, so only that file, matched by its path in the NuGet package folder, drops the ban. Any `[DllImport]` of ours in the same project still fails.

## Services and feature ownership

Services remains one assembly. Each feature folder owns its `I*Backend` seam and platform implementation.

- **A seam describes platform operations the production service actually needs.** Avoid interfaces added only for tests.
- **Only a feature may consume its own backend contracts and implementations.** Another feature uses the service's managed contract, events or shared Core infrastructure. It must not construct, resolve, reference or call a neighboring feature's backend.
- **Composition belongs with the feature that owns the backend.**

Test service policy against fake backends on the test event loop in `tests/TestSupport` (`ManualEventLoop`, or `EventQueue` when the test pumps posted work itself). Fakes supply platform results and signals; the real service supplies the behavior under test. Keep tests that own distinct platform, transport or lifecycle contracts.

Media keeps one type per job, the split from #1102. `ISmtcLink` (#1306) stays the seam `SmtcSession` uses to reach Windows' session.

## Shell

Keep `HostWindow` split by responsibility, with explicit owners for window lifetime, placement, native message handling and presentation. Keep `Shell.cs` and `ShellServices.cs` thin: they compose focused types instead of collecting feature behavior. Preserve disposal order, event-loop affinity, cancellation and the existing public Shell API.

A shell author builds from primitives, building blocks and compositions such as `Bar`, and a composition uses only public API: see [shells are built from public building blocks](adr/0001-shells-are-built-from-public-building-blocks.md). A widget is a plain control the shell style styles: see [widgets are plain controls](adr/0003-widgets-are-plain-controls.md).

## Crash supervision

The CLI supervises a shell in a child process. An unexpected shell exit triggers respawn with backoff; a requested stop ends supervision.

- Keep restart policy, cancellation and child ownership in a focused supervisor rather than in the window host.
- Drive policy time through `TimeProvider` so tests can advance it without sleeping.

### Reload

A reload is not a stop. The two reload paths stay separate modules on purpose: each joins a different pair of processes, and the supervisor sees only its own.

| Path | Joins | Modules |
| --- | --- | --- |
| Supervised | `winshell reload` and the supervisor in `winshell run` | `ShellExit`, `ShellSupervisor` |
| Unsupervised | The shell that quits and the one that takes over | `Shell.Reload`, `--after`, `Instances.WaitForExit` |

On the supervised path, the supervisor starts a reloaded configuration's program itself, so it owns the new child under the same rules as the first. A configuration that calls `shell.Reload()` itself still hands over to an unsupervised process: the supervisor cannot see that start.

### Explorer taskbar recovery

The supervisor owns Explorer taskbar recovery independently of the child. Recovery must run even if the shell cannot execute its cleanup.

- **Restore the taskbar** after a child exits without its own cleanup, before waiting or respawning, after a launch failure, and when supervision stops.
- **Leave it** after a child that exited cleanly, which has restored the taskbar itself, and when the taskbar was already hidden when a child started, which belongs to another shell. Restoring either would take the taskbar from the shell that hid it.
- **A new child acquires its own taskbar state.** A restart must not inherit a stale hidden-taskbar lease.
- **Verify recovery** across crashes, repeated failures and intentional shutdown.

## Distribution and configuration

- **One package.** Keep one `Winshell` library NuGet package. It carries Native, Core, Services and Shell; a configuration must not acquire references to separately published module packages. Keep package and template contracts covered as assembly ownership changes.
- **Templates.** The existing template package remains the delivery mechanism for `dotnet new` templates. `scripts/build-template.ps1` compiles the template sources against the in-repo `Winshell.Shell`, with the repository's build and style policy. The full local check and CI's required Shell suite build run it, so the pack project does not compile them.
- **Formatting.** Formatting verifies C# files without loading or building projects; builds own style and analyzer enforcement.
- **User configuration stays TOML**, in [one preferences file per shell](adr/0002-one-preferences-file-per-shell.md). No code reads the files from before that format. Do not add a second configuration system or compatibility layer.

## Architecture proof

`Winshell.Architecture.Tests` runs in the unified suite. Each boundary has one guard:

- **Interop outside Native** (interop attributes, `NativeLibrary` and `Marshal.GetDelegateForFunctionPointer`): the build. RS0030 from `BannedSymbols.Interop.txt`, added by `Directory.Build.targets`.
- **Unsafe code outside Native, in a project without WinUI:** the build. CS0227, since `Directory.Build.targets` turns `AllowUnsafeBlocks` off.
- **Hand-written unsafe code in a WinUI project:** `WinUISourceTests`. It parses the C# files of each `UseWinUI` project in `winshell.slnx`, outside `bin` and `obj`, and the files it links, for `unsafe`, `fixed` and pointer types.
- **Layer order:** `LayerTests`. Each layer's project references and its compiled assembly's Winshell references name only layers below it. Restore already fails on an upward project reference that closes a cycle (MSB4006); the test catches the rest, such as a reference to a project outside the layers or to a built DLL.
- **Feature backends:** `FeatureBackendTests`. In the compiled Services and Shell, only types in an `I*Backend` interface's own namespace in Services, such as `Winshell.Session`, name the interface or a type that implements it, in a declaration, signature or method body.
- **Compositions use only public API** ([ADR 0001](adr/0001-shells-are-built-from-public-building-blocks.md)): `CompositionTests`. It compiles the C# files in `src/Winshell.Shell/Compositions`, such as `Bar`, again as an assembly of their own against the built Winshell assemblies, which shows them only the public API, and fails on any compiler error. In Shell itself the compiler lets them see Shell's internals and those Core and Services show Shell.

How the tests work:

- **The tests read the project files and the compiled assemblies**, never a list of expected files, so a new project, reference, backend or composition is covered without editing them. Only a new layer is added to `LayerTests`' list.
- **The CLI, `src/Winshell`, is outside the tests' guards:** it composes the layers.
- **Two guards read sources.** A WinUI project's assembly can't tell CsWinRT's generated unsafe code from ours, so that guard reads the sources. The composition guard reads them too, because only the compiler can say what a file may see.
- **Prove a new or changed guard.** Show that it rejects a representative forbidden dependency or declaration, then remove that violation. Do not add production hooks solely to exercise the guards.

Tests follow [where a test belongs](../CONTRIBUTING.md#where-a-test-belongs), which says what a changed test keeps, and the [test rules](../CONTRIBUTING.md#write-tests-that-dont-flake). Use fake clocks and real completion signals.
