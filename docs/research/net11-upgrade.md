# .NET 11 upgrade investigation

Research date: 2026-11-11. Repository baseline: `988e56c5` on `origin/main`. Tracks [#2053](https://github.com/enpaze/winshell/issues/2053). This report records research and disposable probes; it does not change Winshell's target framework or dependencies.

While the probes ran, #2052 landed as `981ea05d`. This documentation branch was rebased onto it and the referenced process/framework configuration was checked again. Execution results below remain evidence for `988e56c5`; the later instance/IPC changes were not rerun on .NET 11.

## Recommendation

**A .NET 11 migration is feasible, but a version-only update fails and shipping is not yet validated.** An isolated copy builds cleanly with the final SDK after collection-expression compatibility edits and two cancellation-token decisions. Core and Services tests pass, both release architectures publish, and the published x64 CLI starts. Broader CLI/supervision validation remains unresolved; shipping also requires package, desktop and hot-reload validation below.

Keep .NET 10 for the current release while making a separate, focused migration ticket. Adopt .NET 11 without bundling feature rewrites, a test-runner migration or preview language/runtime switches. Then consider the process APIs first; they address complexity Winshell already owns.

Microsoft's [11.0 release metadata][release11] lists .NET 11.0.0 and SDK 11.0.100 as released on **2026-11-10**, with active STS support through **2028-11-14**. The [10.0 metadata][release10] lists LTS support through the same date. Moving now does not extend Winshell's support horizon.

## Current project constraints

These are repository observations, not upstream support promises:

| Area | Current configuration | Upgrade implication |
| --- | --- | --- |
| SDK and framework | `global.json`: SDK 10.0.403, patch roll-forward. `Directory.Build.props`: `net10.0-windows10.0.22621.0`, `LangVersion=latest`, `AnalysisLevel=10.0`, warnings as errors. | SDK 11 alone does not change the framework. Keep the Windows platform suffix. Deliberately control language and analyzer adoption. |
| UI | Windows App SDK WinUI/Foundation/Runtime 2.6.4 and Reactor 0.1.0-preview.17 in `Directory.Packages.props`. | Preserve the coordinated Windows App SDK pins. Reactor's restored package has a .NET 10 Windows asset; successful restore and compilation show it can be consumed here, not that every runtime path is certified. |
| Native code | `Winshell.Native` owns CsWin32, COM, handles and unsafe operations. WinUI projects allow generated unsafe code through `Directory.Build.targets`. | Retain that boundary. New unsafe APIs are not a reason to allow hand-written interop elsewhere. |
| Distribution | `src/Winshell/Winshell.csproj` trims partially, roots COM/WinRT/reflection dependencies and enables built-in COM interop. `scripts/package.ps1` publishes self-contained ReadyToRun for x64 and Arm64. | A Debug build cannot prove the shipped application works. Keep roots and publish settings through the first migration. |
| Public package | `src/Winshell.Package/Winshell.Package.csproj` packs assemblies under `lib/$(TargetFramework)`. The starter explicitly targets .NET 10 and is self-contained. | A .NET 11-only Winshell package requires author configurations to retarget; update templates, examples, widget guidance and package tests together. Existing self-contained releases retain their bundled runtime. |
| Development | `winshell dev` runs `dotnet watch`; `DevOutput.cs` parses its messages. | Check watcher output, crash recovery, rude edits and Reactor hot reload with the final SDK. |
| Tests | `tests/Directory.Build.props` uses xUnit v3 with `xunit.v3.mtp-off`, Test SDK and the VSTest adapter. `scripts/lib/Tests.ps1` relies on VSTest filters, TRX and hang options. | Keep the current runner. Most newly advertised testing options are MTP-specific. |
| CI | `.github/actions/prepare/action.yml` installs the SDK from `global.json`. | It follows the SDK pin; no independent `dotnet-version` replacement is needed in that action. |

The Windows App SDK and Reactor are separately versioned products. A .NET upgrade does not itself bring new WinUI controls, Reactor behavior or Windows features. See the [Windows App SDK release channels][winappsdk] and [Reactor source][reactor].

**The SDK also updates implicit Windows dependencies.** The baseline resolves Windows SDK .NET projections `10.0.22621.57` with `WinRT.Runtime` 2.2.0; the .NET 11 probe resolves `10.0.22621.87` with `WinRT.Runtime` 2.3.1. These versions were verified in restored assets and the packages' `data/RuntimeList.xml`. SDK 11's [bundled-version mapping][bundled11] and [version properties][manual11] explain the change. The mapping can affect .NET 10 Windows projects built with SDK 11 too; that SDK-only case was inferred from the targets, not separately built here.

Keep the platform version ending in `.0`. The `.1` opt-in selects CsWinRT 3 projections and already exists in SDK 10; it is not a new .NET 11 benefit. CsWinRT 3 remains preview, and the current Windows App SDK pins ship CsWinRT 2 projections. Do not bundle that transition into this migration. [SDK mappings][bundled11], [CsWinRT package versions][cswinrtversions].

## Compatibility probes

The probes used Windows 11 build 26200, x64, and a `git archive` of the baseline in a temporary directory. The final Windows x64 SDK zip came from [Microsoft's release metadata][release11]; its SHA-512 matched that metadata. The installed Preview 7 SDK was not used for these results. No SDK was installed system-wide.

The .NET 11 copy changed SDK 10.0.403 to 11.0.100 and `net10.0` to `net11.0` in project/framework declarations and test fixtures. Dependency versions and `AnalysisLevel=10.0` remained unchanged. The production checkout remains untouched.

| Probe | Observed result | What it establishes |
| --- | --- | --- |
| Baseline: `dotnet build winshell.slnx --nologo -warnaserror` on SDK 10.0.403 | Passed, zero warnings/errors. | The original tree builds on its pinned toolchain. |
| Same build on SDK 11.0.100 after framework changes | Failed: 99 `CsWinRT1032` errors and two `CA2016` errors. | Framework and SDK substitutions alone are insufficient. |
| .NET 11 build with `-p:LangVersion=14.0` | Same 101 errors. | Keeping the old language version does not resolve these diagnostics. |
| .NET 11 build after explicit array casts and cancellation choices | Passed, zero warnings/errors. | The observed compilation blockers have straightforward source fixes with the existing package pins. |
| Core tests: `dotnet test tests/Winshell.Core.Tests --no-build --filter 'Category!=Window&Category!=Package'` | 154 passed, zero failed/skipped. | Core contracts exercised by this suite work on the new runtime. |
| Services tests: same command for `tests/Winshell.Services.Tests` | 969 passed, zero failed/skipped. | Non-window service contracts exercised by this suite work on the new runtime. |
| Shell tests with `Category!=Window&Category!=Package` | 142 passed; one documentation/sample consistency test failed. | The probe's array casts changed compiled guide samples without matching documentation edits. This is probe-induced drift, not evidence of a runtime defect; the real migration must update both. |
| CLI `DevOutputTests`, `SupervisionTests` and CLI JSON filter | Did not finish after more than five minutes; the owned test process tree was stopped. | No pass result. The cause was not diagnosed, so supervision remains an upgrade validation gate. |
| Isolated `DevOutputTests`, with a one-minute hang guard | 16 passed, zero failed/skipped. | Existing parser contracts pass; this does not exercise a live watcher. |
| `dotnet publish src/Winshell -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true` and the equivalent `win-arm64` command | Both passed with the project's partial trimming enabled. | Both release architectures can be produced; this is not full installer/MSIX validation. |
| Published x64 `winshell.exe --help` | Passed, exit code 0, expected help output. | The published CLI starts on the probe machine. This is not a runtime-free machine or desktop-shell test; the Arm64 binary was not executed. |

The first successful adjustment required **123 explicit array casts**: the original 99 diagnostics, then 24 more in CLI tests once their dependency compiled. One cast needed nullable elements to preserve the existing test data. Both `Console.Error.WriteLineAsync` calls in `src/Winshell/ShellSupervisor.cs` received an explicit `CancellationToken.None` in the probe; the real migration must confirm that intended shutdown behavior.

`CsWinRT1032` reports collection expressions targeting read-only interfaces or `IEnumerable<T>` whose concrete types cannot be generated safely for trimmed WinRT scenarios. The diagnostic itself recommends a concrete type or explicit array cast. Affected files include `src/Winshell/Commands/WidgetCommand.cs`, `examples/omarchy-style/AppShell.cs`, `scripts/generate-reference.cs`, architecture tests and many Shell/CLI tests. This is a measured compatibility issue, not a claim that C# 15 introduced the diagnostic. Do not suppress it to make the migration pass. See the [CsWinRT diagnostic definitions][cswinrt].

## Benefits worth considering

Priority describes usefulness to current Winshell code, not a benchmark result. No CPU, memory, startup or package-size improvement has been measured for Winshell.

### First follow-up: simplify process execution

[.NET 11 process APIs][process11] directly overlap existing responsibilities:

| New capability | Where it could help | Constraint to preserve |
| --- | --- | --- |
| `Process.RunAndCaptureText[Async]`, `Run[Async]`, `ReadAllLines[Async]` | `src/Winshell.Services/Config/ConfigurationProject.cs` currently runs two reader threads and keeps combined plus separate output. Widget installation also runs git/dotnet through it. | Preserve bounded process and stream waits, output limits, cancellation, error classification and mixed stdout/stderr handling. A helper that waits for inherited handles to close can reintroduce a hang. Inspect overload semantics before replacement. |
| `ProcessStartInfo.InheritedHandles`, explicit standard-stream handles and `File.OpenNullHandle` | `src/Winshell.Services/Instances/InstanceStart.cs` clears inheritance on the parent's standard handles through `src/Winshell.Native/Instances/ProcessConsole.cs`. | Investigate child-specific handle control as a way to remove parent-handle mutation. Verify redirected callers and background shells outliving the caller. |
| `StartAndForget`, `StartDetached` | `InstanceStart.Detach` and launch helpers. | Windows first-show state, console independence and process supervision are distinct contracts. Do not assume similarly named APIs implement all of them. |
| `TryGetProcessById`, `SafeProcessHandle` lifecycle APIs | Exited-process lookups in `Instances.cs`, `AtomicFile.cs`, `ShellExit.cs`, `DoctorProbe.cs`, `ExplorerTaskbarBackend.cs` and `Audio/SessionNode.cs`. | Keep process-name/start-time checks: a PID can be reused. A successful lookup is not proof of identity or permission to read every property. |
| `KillOnParentExit`, suspended starts | Possible future strengthening of `src/Winshell/ShellProcess.cs` and shell supervision. | Abrupt termination cannot replace graceful shutdown, crash recovery or detached supervision. No current change is justified just by API availability. |

Start with one bounded command-execution contract and its existing tests. Do not redesign process supervision and command execution together.

These APIs also appear in the [final process API diff][processdiff11]; `File.OpenNullHandle` is in the [final runtime API diff][runtimediff11]. Child-specific handle behavior is an inference from the [Windows implementation][processsource11], not a demonstrated replacement for Winshell's detach contract. `KillOnParentExit` is also worth considering for the `dotnet watch` child in `DevSession.cs`, where a hard-killed parent cannot run cleanup; test nested job/MSBuild-node behavior before adoption.

### Low-risk gains to measure after retargeting

| Change | Winshell location and likely benefit | Decision |
| --- | --- | --- |
| JIT bounds-check elimination, constant folding and devirtualization | `src/Winshell.Services/Images/ImageHeader.cs`, image/capture loops, collection synchronization and backend interface calls may generate less work. [Runtime changes][runtime11]. | Take normal runtime improvements; do not rewrite loops to chase an unmeasured gain. |
| Async continuation overhead reductions | `StateReader.cs`, `DeviceAction.cs`, IPC, Winland and media operations use tasks and `ConfigureAwait(false)`. [Runtime changes][runtime11]. | Measure allocation/latency under event bursts. Distinguish normal runtime/BCL gains from opting Winshell's methods into Runtime Async. |
| ReadyToRun specialization of default comparers | Shipped R2R builds use dictionaries/sets and sorting throughout services. [R2R changes][r2r11]. | A more direct match to Winshell's distribution than JIT-only examples; measure startup and active workloads. Windows multi-CPU-group GC fixes are also useful on affected hardware, with no special feature adoption. |
| NativeAOT CLI front end and warm MSBuild server | Repeated configuration builds, widget commands and development checks invoke `dotnet`. [SDK changes][sdk11]. | Potentially shorter command startup; verify output capture and parent/child lifetimes with the server enabled by default. This does not make Winshell NativeAOT. |
| Folder-mode formatting discovery | `scripts/format.ps1` uses `dotnet format whitespace --folder` with selected files. [SDK changes][sdk11]. | It can avoid unrelated directory scans. Check formatting output remains stable. |
| `dotnet watch` crash recovery | `src/Winshell/Commands/DevCommand.cs`, `DevOutput.cs`, `DevSession.cs` and `tests/Winshell.Tests/DevCommandTests.cs`. [SDK changes][sdk11]. | Retain Reactor's own hot-reload integration and Winshell's supervisor; test the combination rather than removing either based on the announcement. |
| ZIP entry CRC32 verification | Widget package extraction in `WidgetCommand.cs` and `tests/Winshell.Tests/WidgetCommandTests.cs`. [Compatibility note][zipcrc]. | Useful corruption detection. Check that malformed package failures stay actionable; checksum validation is not signature/trust verification. |

### Targeted library opportunities

The [library release notes][libraries11] contain the following potentially relevant additions. Each is optional and should earn a focused change:

| Feature | Where and why | Priority |
| --- | --- | --- |
| JSON Lines output through `JsonSerializer.SerializeAsyncEnumerable(..., topLevelValues: true)` | `src/Winshell.Services/Ipc/IpcServer.cs` serializes each response to a string before writing a line; event streams could write directly using source-generated type metadata. Preserve per-message flushing, cancellation, limits and one-line framing. This API serializes a sequence; it is not a direct replacement for every single-response `Send`. [JSON notes][json11]. | Medium; benchmark before replacing the simple writer. |
| JSON source-generation/contract improvements and writer reuse | `IpcJson.cs`, CLI JSON and instance records already use generated contracts. Check changed diagnostics and metadata behavior; use new contract controls only for a concrete wire-format need. Union serialization would matter only if a future model actually adopts unions. [JSON notes][json11]. | Medium for validation, low for redesign. |
| `ReadOnlyMemoryStream`, `WritableMemoryStream`, `ReadOnlySequenceStream`, `StringStream` | Image, resource and JSON boundaries that genuinely copy memory only to obtain a stream could benefit. Existing `new MemoryStream(byte[])` already wraps an array, and growable encoders such as `PixelImage.EncodePng` still need output storage. [Stream adapters][streams11]. | Low until an actual avoidable copy is identified. |
| Rune-aware string operations and UTF validation/search | App names, command text and `src/Winshell.Core/Crashes/CrashRedaction.cs` might benefit when handling supplementary Unicode or malformed UTF input. Current image-size and IPC line limits remain necessary. [Text APIs][libraries11]. | Low; use for a demonstrated correctness requirement. |
| Regex newline handling and generator fixes | Crash redaction, configuration build errors and development output parse text from several sources. Avoid widening a security-sensitive regex without tests. [Regex changes][regex11]. | Low. |
| Comparer key-selector factory and `FullJoin`/tuple joins | `src/Winshell.Services/ObservableSync.cs`, app catalogs and other keyed reconciliation code are candidates only if the operation preserves stable object identity, order and notifications. `LeftJoin`/`RightJoin` already existed in .NET 10. [LINQ changes][linq11]. | Low; no bulk LINQ conversion. |
| Reflection helpers, including `Type.GetNullableUnderlyingType` | `src/Winshell.Core/Config/TomlKeys.cs` unwraps nullable types; the new helper could express that directly. It does not remove Tomlyn/reflection trimming requirements. [Reflection changes][libraries11]. | Low; little migration value. |
| Safe file/pipe handle APIs and `RandomAccess` pipe support | Relevant only if `PipeTrust.cs`, process output or native pipe ownership needs a lower-level handle contract. Existing named-pipe transport is already appropriate. [I/O changes][libraries11]. | Low; retain the current abstraction. |
| `FORCE_COLOR` | `src/Winshell/Commands/DoctorCommand.cs` explicitly respects redirected output and `NO_COLOR`. Coordinate any change to that policy rather than assuming `Console` support updates it automatically. [Console changes][libraries11]. | Low. |

### C# 15: adopt selectively

The [C# 15 documentation][csharp15] lists collection-expression arguments, unions, closed hierarchies, extension indexers, labeled loop exits and non-virtual static interface members on older runtimes. Memory-safety work is explicitly preview. `LangVersion=latest` means the existing project picks up the SDK's latest released language version automatically.

| Feature | Possible Winshell use | Recommendation |
| --- | --- | --- |
| Unions and closed hierarchies | Small internal result models such as `IpcClient`'s read outcomes, capture outcomes or `DeviceAction`'s success/failure distinctions could become exhaustive. | Consider only where impossible combinations are causing bugs. Unions box value-type cases, so do not assume an allocation win. Public extension points must remain open; a public API or JSON change is separate. [Union representation][union15]. |
| Collection-expression constructor arguments | Collections requiring an explicit comparer/capacity, including app identity sets and dictionaries. | Modest readability improvement. Account for the measured CsWinRT collection-expression restrictions first. |
| Extension indexers | No compelling current use; Reactor's UI DSL and ordinary collections already have clear interfaces. | Skip speculative convenience APIs. |
| Labeled `break`/`continue` | Nested parsing/scanning loops in `ImageHeader.cs` or widget file discovery. | Use only if it makes a particular algorithm clearer; no mechanical rewrite. |
| Non-virtual static interface members on older runtimes | Winshell already requires a modern runtime. | No material benefit here. |
| Preview memory safety | `src/Winshell.Native`, generated interop and pointer ownership are adjacent concerns. | Keep preview flags off. Continue using the existing sealed interop boundary and CsWin32. |

Runtime Async is **separate** from ordinary C# 15 adoption. The [runtime page][runtimeasync] still marks application opt-in as preview and describes `Features=runtime-async=on`; removal of the `EnablePreviewFeatures` requirement does not make that opt-in stable. Runtime libraries themselves use it. Test explicit application opt-in separately against WinRT awaits, UI synchronization, cancellation, exceptions, Reactor hot reload and trimmed ReadyToRun before considering it for production.

## Compatibility risks beyond compilation

Review the [breaking-change index][breaking11] and [compiler changes][compiler11] at implementation time; the index explicitly says it is incomplete.

- **Hardware and ReadyToRun:** .NET 11 requires x86-64-v2 on x64. Windows Arm64 retains an Armv8.0-A runtime baseline, including non-LSE devices, according to the [hardware compatibility note][hardware11] and [final runtime source][hardwarecode11]. Default Windows R2R targets increase to x86-64-v3 and Armv8.2-A + RCPC. Those R2R targets are not blanket runtime requirements: supported lower-capability devices can incur extra JIT/startup work. Winshell's supported Windows 11 hardware should meet the runtime baseline, but validate VMs and supported Arm64 devices.
- **Trimmed COM/WinRT:** keep the explicit roots in `Winshell.csproj`, the current partial trimming mode and generated WinRT settings. Passing compilation and non-window tests cannot prove UI activation, COM vtables, notifications or a packaged shell work.
- **SDK behavior:** default MSBuild server reuse and changed CLI output matter to `ConfigurationProject.Run`, `DevOutput` and tooling. NuGet restore no longer searches the global package cache for an opportunistically higher version; verify centrally pinned dependencies restore from configured feeds. [SDK breaking changes][breaking11].
- **Watcher messages:** the [final watcher source][watchsource11] retains the prefix/emoji controls and success/error/restart messages parsed by `DevOutput`. That source check and 16 passing parser tests reduce concern about text parsing; they do not prove live hot reload or the unresolved supervision probe.
- **Cancellation overloads:** the observed `CA2016` errors show new API overloads can affect existing source even with analyzer level 10.0. Do not assume that pin freezes diagnostics from WinRT or all analyzer/API interactions.
- **Targeted library behavior:** ZIP CRC checks can add exceptions for corrupted content. `Nullable.GetUnderlyingType` changes concern custom `Type` implementations; `TomlKeys` uses ordinary runtime types. Command-line index-zero changes do not affect the current calls that skip index zero. Winshell's idle/input code calls native `GetTickCount`, so the managed `Environment.TickCount` change does not automatically require rewriting it. See the [library compatibility list][breaking11].
- **WMI and testing packages:** the WMI disposal change concerns generated strongly typed wrappers; `BrightnessBackend.cs` uses `ManagementObjectSearcher` directly, so it does not hit that change. VSTest's Newtonsoft.Json removal is not a reason to switch runners or add a new JSON dependency speculatively. [Compatibility index][breaking11].
- **Concurrency:** preserve the deliberately disabled TieredPGO and concurrent GC in `src/Winshell.Package/buildTransitive/Winshell.props`. Re-measure idle CPU before changing those independently tuned choices.

## Reviewed areas with no current adoption case

The [overview][overview11], [libraries][libraries11] and [SDK notes][sdk11] also cover these areas. They do not justify new Winshell subsystems:

- ASP.NET Core, Blazor, Razor/static assets, EF Core, MAUI/mobile tooling, WPF/Windows Forms UI improvements and WebAssembly: Winshell is a WinUI desktop application, not one of those stacks.
- Container/Podman publishing and reduced Linux/macOS SDK size: no Winshell container deployment or non-Windows runtime.
- IEEE decimal types, generic complex numbers, tensor changes, generic random APIs, specialized SIMD/FP16 and cryptography additions: no current workload requiring them. Capture uses Windows/Direct3D/Media Foundation; there is no evidence for replacing it with a new vectorized managed implementation.
- Zstandard, TAR, ZIP password support and streamless compressor APIs: retain the existing distribution formats unless a separate packaging need arises.
- DNS, QUIC priorities, HTTP connection policies/request compression and experimental TLS sessions: Windows network state services are not an HTTP server or transport library. Normal security fixes are useful without adopting transport APIs.
- MemoryCache/HybridCache metrics, DI, configuration binding, Options, async DataAnnotations, source-generated logging and Activity rules: Winshell uses its own small event loop, TOML preferences, log and caches. These releases are not a reason to replace them with Microsoft.Extensions infrastructure.
- Solution filters and new test templates: the existing `.slnx` and runner/sharding scripts already define the intended graph. New MTP flags, mobile tests and affected-test experiments need a separate justification.
- In-process crash logging documented for Unix/mobile does not replace Winshell's Windows crash reports, redaction and instance follow-up.

File-based app improvements (`#:include`, DLL references, quoted directives, format support) are useful if `scripts/generate-reference.cs` or `scripts/render-logo.cs` grows. They already disable NativeAOT; keep their present structure unless splitting them answers a real maintenance need. NuGet minimum publication age is potentially useful for agent-authored configuration restores, but requires an explicit source policy and must not interfere with local package validation. [SDK notes][sdk11].

## Smallest future migration and release gate

1. Start a build-area ticket from the latest main. Coordinate `Directory.Packages.props`, `global.json` and other shared files with open PRs. Keep package updates out unless a reproducible compatibility issue requires one.
2. Pin final SDK 11.0.100 or its current servicing patch; retarget the shared framework, starter, standalone samples/examples and hard-coded test fixtures. Keep Windows 11 22H2 as the platform floor. Update prerequisite/build/configuration docs and communicate the package's new author SDK/framework requirement.
3. Resolve CsWinRT diagnostics with explicit concrete collection types; preserve nullable element contracts. Make cancellation intent explicit in the two supervisor writes. Keep `AnalysisLevel=10.0` initially; raising analyzer policy can be its own small change.
4. Run the repository's full non-window check, documentation/reference generation, template build and packed-template tests. Validate a new configuration and a widget against the packed .NET 11 package, not only project references. Check old .NET 10 configurations receive a clear upgrade requirement.
5. Publish the actual self-contained, trimmed ReadyToRun payload for both `win-x64` and `win-arm64`. Verify CLI startup from the publish directory on a machine without a .NET 11 installation; inspect expected resource/native files and run package/MSIX validation.
6. Run CI's windowed suites and a deliberate live shell: startup, scale/screens, bars and popup windows, capture, tray, notifications, media/audio and graceful shutdown. Test `winshell dev` for a Render edit, a rude edit, compile-error recovery and recovery after a crash. Verify a trimmed release, not just Debug.
7. Record startup, idle CPU, working set and package size against .NET 10 using the same configuration/hardware. Leave PGO/GC choices alone unless those measurements justify a separate change.
8. Ship the framework migration after those checks; follow with bounded process-API simplification. Evaluate JSONL and preview Runtime Async independently.

## Source and documentation inconsistencies

- Microsoft's [release metadata][release11] says final 11.0.0 is available; the [overview][overview11], runtime/library/SDK pages still identify their feature inventory as RC2, and the [C# page][csharp15] still calls the SDK prerelease. Use final release metadata for availability, and verify optional feature semantics against final APIs before implementing them.
- The RC2 [runtime overview][runtime11] says Windows Arm64 requires LSE, whereas the [hardware compatibility note][hardware11] explicitly retains Armv8.0-A including non-LSE hardware. The [final runtime source][hardwarecode11] distinguishes Apple's LSE baseline from the other Arm64 baseline. This report follows the compatibility note and final implementation; do not confuse the higher R2R target with the runtime floor.
- The library page calls union scaffolding not directly user-facing yet, while the C# page documents released-language union syntax. Treat compiler syntax, runtime scaffolding and serializer support as separate contracts; do not infer a whole-program redesign from either summary.
- `LangVersion=latest` tracks the compiler, independently of the TFM. An SDK-only upgrade can therefore enable C# 15 while still targeting .NET 10; new syntax may require .NET 11 support types. The migration should choose an explicit language version or the TFM's default rather than assume the framework pin freezes it. Microsoft's [language-version guidance][languageversion] advises against `latest`.
- Runtime Async's removed preview attributes on framework helpers do not imply application compiler opt-in is enabled by default. The [final API diff][runtimediff11] and [runtime feature page][runtimeasync] describe different parts of that contract. Likewise, the RC-era NativeAOT optimized-publish breaking-change note differs from the [final SDK release note][sdkga11], which says optimized publish is opt-in again. Winshell uses R2R, not NativeAOT.
- No repository documentation-versus-code contradiction was found in the .NET target, WinUI stack, self-contained packaging or test-runner setup examined. The successful dependency restore/build is narrower evidence than an upstream .NET 11 compatibility guarantee.

[release11]: https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json
[release10]: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
[overview11]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview
[runtime11]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime
[runtimeasync]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime#runtime-async
[hardware11]: https://learn.microsoft.com/en-us/dotnet/core/compatibility/jit/11/minimum-hardware-requirements
[libraries11]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries
[process11]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries#process-api-expansion
[json11]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries#systemtextjson-improvements
[streams11]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries#in-memory-stream-adapters
[regex11]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries#regular-expression-improvements
[linq11]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries#linq-join-improvements
[sdk11]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/sdk
[csharp15]: https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15
[compiler11]: https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/breaking-changes/compiler%20breaking%20changes%20-%20dotnet%2011
[breaking11]: https://learn.microsoft.com/en-us/dotnet/core/compatibility/11
[zipcrc]: https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/11/ziparchive-entry-crc32-validation
[cswinrt]: https://github.com/microsoft/CsWinRT/blob/master/src/Authoring/WinRT.SourceGenerator/AnalyzerReleases.Shipped.md
[winappsdk]: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels
[reactor]: https://github.com/microsoft/microsoft-ui-reactor
[bundled11]: https://github.com/dotnet/dotnet/blob/297acf930fb0f5c07fd4e51d78e521b4ad2e5d1f/src/sdk/src/Layout/redist/targets/GenerateBundledVersions.targets
[manual11]: https://github.com/dotnet/dotnet/blob/297acf930fb0f5c07fd4e51d78e521b4ad2e5d1f/src/sdk/eng/ManualVersions.props
[cswinrtversions]: https://api.nuget.org/v3-flatcontainer/microsoft.windows.cswinrt/index.json
[processdiff11]: https://github.com/dotnet/core/blob/main/release-notes/11.0/11.0.0/api-diff/Microsoft.NETCore.App/11.0.0_System.Diagnostics.Process.md
[runtimediff11]: https://github.com/dotnet/core/blob/main/release-notes/11.0/11.0.0/api-diff/Microsoft.NETCore.App/11.0.0_System.Runtime.md
[processsource11]: https://github.com/dotnet/dotnet/blob/297acf930fb0f5c07fd4e51d78e521b4ad2e5d1f/src/runtime/src/libraries/System.Diagnostics.Process/src/Microsoft/Win32/SafeHandles/SafeProcessHandle.Windows.cs
[r2r11]: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime#readytorun-improvements
[union15]: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union
[watchsource11]: https://github.com/dotnet/dotnet/blob/297acf930fb0f5c07fd4e51d78e521b4ad2e5d1f/src/sdk/src/Dotnet.Watch/Watch/UI/IReporter.cs
[languageversion]: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/configure-language-version
[sdkga11]: https://github.com/dotnet/core/blob/main/release-notes/11.0/11.0.0/sdk.md
[hardwarecode11]: https://github.com/dotnet/dotnet/blob/297acf930fb0f5c07fd4e51d78e521b4ad2e5d1f/src/runtime/src/coreclr/vm/codeman.cpp
