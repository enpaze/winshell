# Widgets are plain controls, styled by the shell style

A widget is any WinUI control whose class takes a `WidgetContext`. Winshell has no widget base class. A widget stays current with `this.Watch(...)` (#1706), which runs its block again when anything the block read changes and stops with the control. It looks right without looking anything up, because the shell style holds implicit styles for plain controls. One shape covers a status icon, a clock, workspaces and the tray alike, and the only Winshell-specific things to learn are `Watch`, the style and `WidgetContext`. A sketch of the API this decides, not code the solution compiles:

```text
public sealed class BatteryWidget : Button
{
    public BatteryWidget(WidgetContext context)
    {
        var shell = context.Shell;
        this.Watch(() =>
        {
            var battery = shell.Power.DisplayDevice;
            Visibility = battery is null ? Visibility.Collapsed : Visibility.Visible;
            if (battery is null)
                return;
            Content = Glyphs.Battery(battery.Percentage, battery.Charging);
            ToolTipService.SetToolTip(this, $"Battery {battery.Percentage}%");
        });
        Click += (_, _) => shell.Open("ms-settings:batterysaver");
    }
}
```

The style is one XAML resource file, set once with `shell.Style`, and a component can take its own. With none set, a built-in style follows Windows: its theme colors, Segoe UI and Segoe Fluent Icons. A style defines six keys every bar, widget area, widget and styled popup reads, and implicit styles for `Button`, `TextBlock`, `FontIcon` and `ToolTip`. So an installed widget matches whatever shell it lands in. Tooltips follow the shell's style, not a component's: a tooltip opens in a popup of its own, and WinUI looks up its style only in the app's resources, so the `ToolTip` entry of `shell.Style` styles every tooltip in the shell. `shell.Open(uri)` opens a Settings page, an app or a URL.

## Considered options

- **A `GlyphWidget` base class** with an icon, a tooltip, an alert color and a click. It fits only a single status icon and is a second shape to learn; `Watch` and the implicit styles cover it.
- **Keys for each component**, such as `BarBackground`. A style would then not carry over to a dock or to an installed widget.

## Consequences

- A WinUI `Style` is one entry of a style. The glossary's Style is the whole look.
- Proposed keys, settled in review: `ShellBackgroundBrush`, `ShellForegroundBrush`, `ShellAlertBrush`, `ShellFontFamily`, `ShellFontSize` and `ShellSlotSize`.
