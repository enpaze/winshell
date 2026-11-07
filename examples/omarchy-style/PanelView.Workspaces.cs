using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Winshell.Winland;

namespace OmarchyStyle;

public sealed partial class PanelView
{
    private void ShowWorkspaces(Watcher watch)
    {
        var winland = watch.Read(_shell.Winland);
        Workspaces.Children.Clear();
        // Winland numbers workspaces per monitor, so the panel shows its own screen's: 1 to 5 always, and any other
        // that exists. None while Winland isn't running or has no monitor for this screen.
        if (winland.ProcessId == 0)
        {
            Workspaces.Visibility = Visibility.Collapsed;
            return;
        }

        var monitor = winland.MonitorFor(_screen);
        if (monitor is null)
        {
            Workspaces.Visibility = Visibility.Collapsed;
            return;
        }

        Workspaces.Visibility = Visibility.Visible;
        watch.Read(monitor);
        var own = watch.Read(winland.Workspaces).Where(w => watch.Read(w).Monitor == monitor && w.Id > 0).ToList();
        foreach (var id in WorkspaceNumbers(own))
        {
            var workspace = own.Find(w => w.Id == id);
            var shown = monitor.ActiveWorkspace?.Id == id;
            var occupied = workspace is not null && watch.Read(workspace.Windows).Count > 0;
            var button = new Button
            {
                Style = (Style)Resources["WorkspaceButton"],
                Content = shown ? Glyphs.Tiling : id.ToString(CultureInfo.CurrentCulture),
                Opacity = shown || occupied ? 1 : 0.5
            };
            AutomationProperties.SetName(button, "Workspace " + id.ToString(CultureInfo.CurrentCulture));
            button.Click += (_, _) => monitor.ActivateWorkspace(id);
            Workspaces.Children.Add(button);
        }
    }

    private static IEnumerable<int> WorkspaceNumbers(IEnumerable<WinlandWorkspace> workspaces)
    {
        return workspaces.Select(w => w.Id).Concat(Enumerable.Range(1, 5)).Distinct().Order();
    }
}
