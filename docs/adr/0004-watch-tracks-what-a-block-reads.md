# Watch tracks what a block reads

A control stays current with `this.Watch(() => …)`: Winshell runs the block, records every service property and list it read, and runs it again when any of them changes. There are no subscriptions to write, no reads to declare and nothing to dispose. Every public getter of a service reports its read to `ObservableObject`, and every public list of a service is a Winshell `ObservableList<T>` that reports a read when it is enumerated, counted or indexed. One test reads every public property of every service type while tracking and fails on any that does not report, so a new property cannot go untracked.

A sketch of the API this decides, not code the solution compiles:

```text
this.Watch(() => Battery.Content = Glyphs.Power(shell.Power.DisplayDevice, shell.Power.OnBattery));
```

## How it runs

- **Where it lives.** `Watch` is an extension on any WinUI element in `Winshell.Shell`. The tracking it relies on is in `Winshell.Core`, next to `ObservableObject`, so services report reads without WinUI.
- **When it runs again.** Changes coalesce: several in one turn of the loop, such as a device's volume and mute together, run the block once, on the next turn, on the control's thread. A change raised on another thread is posted there. A change the block itself makes while it runs does not run it again.
- **Lifetime.** A watch runs while its control is loaded. It pauses when the control unloads, as content of a hidden popup or a widget moved to another area does, and runs again when the control loads. It stops for good when the control's window closes or is disposed. A configuration never disposes a watch.
- **Errors.** A block that throws is logged under `watch`, with the stack. The watch keeps the reads made before the throw, so the next change runs it again. One broken glyph does not end the shell.
- **Live XAML editing.** An edit replaces the control: the old one unloads, and the new one's constructor starts its own watch. Nothing special is needed.

## Considered options

- **`{x:Bind}` bindings.** They cannot express a computed list, each edit costs a rebuild, and a binding must be stopped by hand.
- **Explicit sources, such as `When(source, …)`.** The watch goes stale the first time a source is forgotten.
- **A watch that stops for good on unload.** WinUI unloads a control each time it leaves the tree, so a reopened popup would show what it showed when it closed.
- **Letting a throwing block reach crash supervision.** A mistake in one widget's text would restart the whole shell.

## Consequences

- The 33 public `ObservableCollection<T>` lists of the services become `ObservableList<T>`, read-only to a configuration. That breaks the public API, which is allowed before 1.0.
- Reading a property costs one check of whether a watch is recording on this thread.
