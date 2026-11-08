using Winshell;

namespace OmarchyStyle;

/// <summary>Omarchy's bar on one edge of every screen, in its Tokyo Night style.</summary>
public sealed class AppShell : ShellConfiguration
{
    private readonly List<(PanelWindow Window, PanelView View)> _panels = [];
    private BarSettings? _bar;
    private bool _placePosted;

    public override void Start(Shell shell)
    {
        // Winshell shows Explorer's taskbar again when the shell quits, however it quits.
        shell.ExplorerTaskbar.Hidden = true;
        _bar = new BarSettings(Path.Combine(shell.ConfigRoot, "bar", shell.ShellId + ".toml"), shell.Loop, shell.Log);
        _bar.PropertyChanged += (_, _) => Schedule(shell);
        shell.Screens.CollectionChanged += (_, _) => Schedule(shell);
        Place(shell);
    }

    // A file save can change the edge and the monitor list in one read. Place once, after both have landed.
    private void Schedule(Shell shell)
    {
        if (_placePosted)
            return;
        _placePosted = true;
        shell.Loop.Post(() =>
        {
            _placePosted = false;
            Place(shell);
        });
    }

    // A screen that comes or goes, or a bar setting that changes, gets every panel made again.
    private void Place(Shell shell)
    {
        foreach (var (window, view) in _panels)
        {
            view.Dispose();
            window.Dispose();
        }

        _panels.Clear();
        var settings = _bar!;
        var fallback = BarLayout.NormalizeEdge(settings.Edge);
        foreach (var screen in shell.Screens)
        {
            // Manufacturer, model and serial only. The display name is renumbered by Windows, so it is not a match.
            var choice = BarLayout.For(screen.Manufacturer, screen.Model, screen.SerialNumber, fallback, settings.Monitor);
            if (choice.Warning is { } warning)
                shell.Log.Warn("bar", warning);
            var layout = choice.Layout.Length == 0 ? "" : $" with layout {choice.Layout}";
            shell.Log.Info("bar",
                $"{screen.Manufacturer} / {screen.Model} / {screen.SerialNumber} on the {choice.Edge}{layout}");

            var view = new PanelView();
            view.Apply(choice.Edge, choice.Layout);
            var vertical = BarLayout.Vertical(choice.Edge);
            // A vertical bar is wide enough for the refusal a widget shows there. Top and bottom stay one row high.
            var thickness = vertical ? (double)view.Resources["PanelSide"] : view.Size;
            var window = new PanelWindow
            {
                Screen = screen,
                Anchors = BarLayout.Anchors(choice.Edge),
                ReservedSize = thickness,
                Content = view
            };
            if (vertical)
                window.Width = thickness;
            else
                window.Height = thickness;
            window.Visible = true;
            _panels.Add((window, view));
        }
    }
}
