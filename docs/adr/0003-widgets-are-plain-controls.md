# Widgets are plain controls, styled by the shell style

Superseded by [UI is built with Reactor](0005-ui-is-built-with-reactor.md).

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

The style is a C# value, set with `shell.Style`, and a bar or a widget area can take its own. With none set, a built-in style follows Windows: its theme colors, Segoe UI and Segoe Fluent Icons. Winshell writes it into the resources a bar, a widget area, a widget and a styled popup read, including `ShellPadding` for the space at a bar's ends, and builds implicit styles for `Button`, `TextBlock`, `FontIcon` and `ToolTip`. So an installed widget matches whatever shell it lands in. A `Surface` is the root of a bar, a dock or any composition: it paints the style's `ShellBackgroundBrush` behind its content in the window's theme, so a style needs no entry for it. Code can't do that itself, as WinUI reads a brush from code in the app's theme. Tooltips follow the shell's style, not a component's: a tooltip opens in a popup of its own, and WinUI looks up its style only in the app's resources, so the `ToolTip` entry of `shell.Style` styles every tooltip in the shell. `shell.Open(uri)` opens a Settings page, an app or a URL.

## Considered options

- **A `GlyphWidget` base class** with an icon, a tooltip, an alert color and a click. It fits only a single status icon and is a second shape to learn; `Watch` and the implicit styles cover it.
- **Keys for each component**, such as `BarBackground`. A style would then not carry over to a dock or to an installed widget.

## Consequences

- A WinUI `Style` is one entry of a style. The glossary's Style is the whole look.
- Proposed keys, settled in review: `ShellBackgroundBrush`, `ShellForegroundBrush`, `ShellAlertBrush`, `ShellFontFamily`, `ShellFontSize` and `ShellSlotSize`.
