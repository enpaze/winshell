using System.Globalization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;
using Winshell.Winland;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

/// <summary>
/// Winland's workspaces on this widget's own screen: 1 to 5 always, and any other that exists. An empty one is dimmed,
/// the shown one is the tiling glyph, and a click shows one. A side bar stacks them.
/// </summary>
public sealed class WorkspacesWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        var (window, setWindow) = UseState<ShellWindow?>(null);
        if (window is null)
            return LoadedWindow.Placeholder(setWindow);
        return Component<Workspaces, WorkspacesProps>(new(window, Props));
    }

    private sealed record WorkspacesProps(ShellWindow Window, WidgetContext Context);

    /// <summary>The workspace buttons, once the bar's window is known.</summary>
    private sealed class Workspaces : Component<WorkspacesProps>
    {
        public override Element Render()
        {
            var window = UseObservable(Props.Window);
            var winland = UseObservableTree(Props.Context.Shell.Winland);
            var vertical = Props.Context.Orientation == Orientation.Vertical;
            // None before Winland is running, or once it reports no monitor for this screen.
            var monitor = winland.ProcessId > 0 ? winland.MonitorFor(window.Screen) : null;
            if (monitor is null)
                return TextBlock("").IsVisible(false);
            return Buttons(monitor, winland, vertical);
        }

        private static Element Buttons(WinlandMonitor monitor, WinlandService winland, bool vertical)
        {
            var own = winland.Workspaces.Where(workspace => workspace.Monitor == monitor && workspace.Id > 0).ToList();
            var buttons = own.Select(workspace => workspace.Id).Concat(Enumerable.Range(1, 5)).Distinct().Order()
                .Select(id =>
                {
                    var shown = monitor.ActiveWorkspace?.Id == id;
                    var occupied = own.Find(workspace => workspace.Id == id)?.Windows.Count > 0;
                    var label = id.ToString(CultureInfo.CurrentCulture);
                    return Button(shown ? Glyphs.Tiling : label, () => monitor.ActivateWorkspace(id))
                        .MinWidth(20)
                        .Opacity(shown || occupied ? 1 : 0.5)
                        .AutomationName("Workspace " + label);
                })
                .Cast<Element?>()
                .ToArray();
            var row = BarLayout.Along(vertical ? Orientation.Vertical : Orientation.Horizontal, 1, buttons);
            return vertical
                ? row.HAlign(HorizontalAlignment.Center)
                : row.Margin(0, 0, 1.5, 0).HAlign(HorizontalAlignment.Left);
        }
    }
}
