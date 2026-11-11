# UI is built with Reactor

Every piece of UI Winshell or a configuration shows is a Microsoft.UI.Reactor component: widgets, bars, widget areas and the Notice. One C# way to describe UI is easier to learn than plain controls, `Watch` and XAML styles together, and it lets Winshell delete the machinery those needed. Winshell keeps its windows: a panel, a popup window or a floating window places and reserves itself as before and shows a mounted component as its content, because Reactor's own windows cannot anchor to an edge, reserve the work area or open next to another window.

```text
public sealed class Battery : Component<WidgetContext>
{
    public override Element Render()
    {
        var battery = UseObservable(Props.Shell.Power.DisplayDevice);
        return TextBlock($"{battery.Percentage:P0}").IsVisible(battery.IsPresent);
    }
}
```

- **A widget is a `Component<WidgetContext>`.** `WidgetContext` is a record of props: the shell, the shell window the area is drawn in, the placement's settings and the area's orientation. A changed placement, or the area moving into a window, renders the widget again with new props. Winshell has no widget base class, and a plain control is no longer a widget: a control Reactor has no element for is embedded through Reactor's XAML interop.
- **A component names what it reacts to** with Reactor's `UseObservable` or `UseObservableTree`, on a service object or an `ObservableList<T>`. Winshell adds no read tracking of its own. An `ObservableList<T>` raises `PropertyChanged` when an item does, and an object raises when a list property it has read changes, so those hooks see a change inside a service. That is the object's own change, not a tracked read.
- **A style is a C# value**, including the alert color, which Winshell writes into the window's resources along with the implicit control styles it builds from it, so a plain `Button` in a widget still matches the shell. No `.xaml` file is left, and `winshell dev` reloads through `dotnet watch` and Reactor's hot reload.
- **Widget areas still create widgets by name** from the placements in the preferences file, so a user can rearrange a shell without code.
- **Reactor is a preview.** Winshell pins one exact version and carries it in the `Winshell` package. A Reactor update that breaks a configuration is released as a Winshell breaking change.

## Considered options

- **Reactor's windows for panels.** They would need edge anchoring, work-area reservation and popup placement, which is the platform work Winshell exists for.
- **Plain controls alongside components.** Two shapes and two update models to learn and maintain.
- **A widget base class whose `Render` tracks what it reads,** keeping the promise of `Watch`. Reactor's model is that every reactive edge is declared; a tracked render would be Winshell's own variant of it, and a child component would not be tracked.
- **A `UseWidget()` hook reading a Reactor context** for the placement's settings. Props are Reactor's way to pass a component its inputs.
- **Composing bars only in code.** It would delete widget areas and the registry, but a user could no longer change a shell from the preferences file.
- **Waiting for a stable Reactor.** Winshell is before 1.0 and can absorb Reactor's breaks.

## Consequences

- This supersedes [widgets are plain controls](0003-widgets-are-plain-controls.md) and [Watch tracks what a block reads](0004-watch-tracks-what-a-block-reads.md). `Watch`, the read tracking in `Winshell.Core` and the test that every service property reports its read, the XAML styles and `XamlReload` go. Services keep `INotifyPropertyChanged`, which `UseObservable` needs.
- "Component" now names a piece of UI. A named part of the shell's preferences file is a section.
- The switch is one breaking release with no compatibility layer. The template, the examples, the samples and the docs move with it.
