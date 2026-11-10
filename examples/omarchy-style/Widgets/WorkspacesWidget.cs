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
        var observe = UseRef(new Observe());
        _ = UseObservable(observe.Current);
        var winland = UseObservable(Props.Shell.Winland);
        _ = UseObservable(winland.Workspaces);
        // MonitorFor reads this list. A screen's monitor can arrive without the workspace list changing.
        _ = UseObservable(winland.Monitors);
        var (window, setWindow) = UseState<ShellWindow?>(null);
        if (window is null)
            return LoadedWindow.Placeholder(setWindow);

        var vertical = Props.Orientation == Orientation.Vertical;
        var monitor = winland.ProcessId > 0 ? winland.MonitorFor(window.Screen) : null;
        var observers = new List<Element>
        {
            Observe.Of(window, observe.Current, "window", () => window.Screen?.Id ?? "")
        };
        if (monitor is not null)
        {
            observers.Add(Observe.Of(monitor, observe.Current, BarLayout.Key(monitor), () =>
                monitor.ActiveWorkspace?.Id.ToString(CultureInfo.InvariantCulture) ?? ""));
            foreach (var workspace in winland.Workspaces)
            {
                var key = BarLayout.Key(workspace);
                observers.Add(Observe.Of(workspace, observe.Current, "ws-" + key, () =>
                    string.Create(CultureInfo.InvariantCulture, $"{workspace.Id}\n{workspace.Monitor?.Name}")));
                observers.Add(Observe.Of(workspace.Windows, observe.Current, "windows-" + key,
                    () => workspace.Windows.Count.ToString(CultureInfo.InvariantCulture)));
            }
        }

        var buttons = monitor is null
            ? TextBlock("").IsVisible(false)
            : Buttons(monitor, winland, vertical);
        return Observe.Show(buttons, vertical ? HorizontalAlignment.Center : HorizontalAlignment.Left, [.. observers]);
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
        return vertical ? row.HAlign(HorizontalAlignment.Center) : row.Margin(0, 0, 1.5, 0);
    }
}
