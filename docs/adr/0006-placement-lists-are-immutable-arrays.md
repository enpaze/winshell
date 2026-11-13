# Placement lists are immutable arrays

A shell writes its placements as collection expressions, such as `Start = ["windows", "workspaces"]` or `WidgetArea("dock", ["start", "tasks"])`. Winshell's public lists of placements are `ImmutableArray<Placement>`, not `IReadOnlyList<Placement>`: `Bar.Start`, `Bar.Center` and `Bar.End`, `WidgetAreaProps.Defaults`, the `defaults` of `Elements.WidgetArea`, what `Placement.Parse` returns and what `Placement.Format` takes. A collection expression that targets a read-only interface has no concrete type, so CsWinRT rejects it in a trimmed WinRT app (`CsWinRT1032`). That is an error in every shell from the .NET 11 SDK on. A concrete type keeps the expressions a shell already has compiling, without a cast.

## Considered options

- **Keep `IReadOnlyList<Placement>`, and have each shell cast its lists to arrays.** Every shell would break, for no gain.
- **Suppress `CsWinRT1032` in the projects that build a shell.** The diagnostic marks code a trimmed WinRT app cannot rely on.
- **Arrays.** Properties that return arrays leak mutable state (CA1819), and a shell could change a bar's placements after building it.
