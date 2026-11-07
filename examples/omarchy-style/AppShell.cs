using Winshell;

namespace OmarchyStyle;

/// <summary>Omarchy's top bar in its Tokyo Night style: one panel along the top of every screen.</summary>
public sealed class AppShell : ShellConfiguration
{
    private readonly List<(PanelWindow Window, PanelView View)> _panels = [];

    public override void Start(Shell shell)
    {
        // Winshell shows Explorer's taskbar again when the shell quits, however it quits.
        shell.ExplorerTaskbar.Hidden = true;
        shell.Screens.CollectionChanged += (_, _) => Place(shell);
        Place(shell);
    }

    // A screen that comes, goes or changes gets every panel made again.
    private void Place(Shell shell)
    {
        foreach (var (window, view) in _panels)
        {
            view.Dispose();
            window.Dispose();
        }

        _panels.Clear();
        foreach (var screen in shell.Screens)
        {
            var view = new PanelView(shell, screen);
            _panels.Add((new PanelWindow
            {
                Screen = screen,
                Anchors = Edges.Left | Edges.Right | Edges.Top,
                Height = PanelView.Size,
                ReservedSize = PanelView.Size,
                Content = view,
                Visible = true
            }, view));
        }
    }
}
