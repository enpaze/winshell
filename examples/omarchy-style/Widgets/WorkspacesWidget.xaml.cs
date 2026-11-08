using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Winshell;
using Winshell.Winland;

namespace OmarchyStyle;

/// <summary>Winland's workspaces on this widget's own screen: 1 to 5 always, and any other that exists.</summary>
public sealed partial class WorkspacesWidget : IDisposable
{
    private readonly Shell _shell;
    private Watcher? _watcher;
    private bool _disposed;

    public WorkspacesWidget(WidgetContext context)
    {
        InitializeComponent();
        _shell = context.Shell;
        Loaded += (_, _) =>
        {
            if (_disposed)
                return;
            _watcher?.Dispose();
            _watcher = new Watcher(Show);
        };
    }

    public void Dispose()
    {
        _disposed = true;
        _watcher?.Dispose();
        _watcher = null;
    }

    private void Show(Watcher watch)
    {
        var screen = Bar.Screen(this, _shell);
        var winland = watch.Read(_shell.Winland);
        Buttons.Children.Clear();
        // None while Winland isn't running, this widget is not in a window yet, or Winland has no monitor for the screen.
        if (screen is null || winland.ProcessId == 0)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        var monitor = winland.MonitorFor(screen);
        if (monitor is null)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        watch.Read(monitor);
        var own = watch.Read(winland.Workspaces).Where(workspace => watch.Read(workspace).Monitor == monitor && workspace.Id > 0)
            .ToList();
        var style = Bar.Resource<Style>(this, "WorkspaceButton");
        foreach (var id in WorkspaceNumbers(own))
        {
            var workspace = own.Find(workspace => workspace.Id == id);
            var shown = monitor.ActiveWorkspace?.Id == id;
            var occupied = workspace is not null && watch.Read(workspace.Windows).Count > 0;
            var button = new Button
            {
                Style = style,
                Content = shown ? Glyphs.Tiling : id.ToString(CultureInfo.CurrentCulture),
                Opacity = shown || occupied ? 1 : 0.5
            };
            AutomationProperties.SetName(button, "Workspace " + id.ToString(CultureInfo.CurrentCulture));
            button.Click += (_, _) => monitor.ActivateWorkspace(id);
            Buttons.Children.Add(button);
        }
    }

    private static IEnumerable<int> WorkspaceNumbers(IEnumerable<WinlandWorkspace> workspaces)
    {
        return workspaces.Select(workspace => workspace.Id).Concat(Enumerable.Range(1, 5)).Distinct().Order();
    }
}
