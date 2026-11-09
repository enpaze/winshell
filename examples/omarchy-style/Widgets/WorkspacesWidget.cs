using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace OmarchyStyle;

/// <summary>
/// Winland's workspaces on this widget's own screen: 1 to 5 always, and any other that exists. An empty one is dimmed,
/// the shown one is the tiling glyph, and a click shows one. A side bar stacks them.
/// </summary>
public sealed class WorkspacesWidget : UserControl
{
    public WorkspacesWidget(WidgetContext context)
    {
        var winland = context.Shell.Winland;
        var vertical = context.Orientation == Orientation.Vertical;
        var buttons = new StackPanel
        {
            Orientation = context.Orientation,
            Spacing = 1,
            Margin = vertical ? new Thickness(0) : new Thickness(0, 0, 1.5, 0)
        };
        Content = buttons;
        if (vertical)
            HorizontalAlignment = HorizontalAlignment.Center;

        this.Watch(() =>
        {
            buttons.Children.Clear();
            // None before the widget is in its bar's window, while Winland isn't running, or when it has no monitor
            // for this screen.
            var screen = ShellWindow.Of(this)?.Screen;
            var monitor = screen is null || winland.ProcessId == 0 ? null : winland.MonitorFor(screen);
            Visibility = monitor is null ? Visibility.Collapsed : Visibility.Visible;
            if (monitor is null)
                return;

            var own = winland.Workspaces.Where(workspace => workspace.Monitor == monitor && workspace.Id > 0).ToList();
            foreach (var id in own.Select(workspace => workspace.Id).Concat(Enumerable.Range(1, 5)).Distinct().Order())
            {
                var shown = monitor.ActiveWorkspace?.Id == id;
                var occupied = own.Find(workspace => workspace.Id == id)?.Windows.Count > 0;
                var button = new Button
                {
                    MinWidth = 20,
                    Content = shown ? Glyphs.Tiling : id.ToString(CultureInfo.CurrentCulture),
                    Opacity = shown || occupied ? 1 : 0.5
                };
                AutomationProperties.SetName(button, "Workspace " + id.ToString(CultureInfo.CurrentCulture));
                button.Click += (_, _) => monitor.ActivateWorkspace(id);
                buttons.Children.Add(button);
            }
        });
    }
}
