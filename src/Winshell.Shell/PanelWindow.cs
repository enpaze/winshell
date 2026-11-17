using Microsoft.UI.Xaml;

namespace Winshell;

public sealed partial class PanelWindow : ShellWindow
{
    private Edges _anchors;
    private Thickness _margins;
    private double _reservedSize;
    private bool _coversScreen;
    private bool _aboveWindows = true;
    private bool _focusable;
    private PanelEdge? _edge;

    public PanelWindow()
        : this(Shell.Current)
    {
    }

    internal PanelWindow(Shell shell)
        : base(shell)
    {
    }

    private protected override WindowKind Kind => WindowKind.Panel;

    public Edges Anchors
    {
        get => _anchors;
        set
        {
            if (Set(ref _anchors, value))
                OnPlacementChanged();
        }
    }

    public Thickness Margins
    {
        get => _margins;
        set
        {
            if (Set(ref _margins, value))
                OnPlacementChanged();
        }
    }

    public double ReservedSize
    {
        get => _reservedSize;
        set
        {
            if (Set(ref _reservedSize, value))
                OnPlacementChanged();
        }
    }

    public bool CoversScreen
    {
        get => _coversScreen;
        set
        {
            if (Set(ref _coversScreen, value))
                OnPlacementChanged();
        }
    }

    public bool AboveWindows
    {
        get => _aboveWindows;
        set
        {
            if (Set(ref _aboveWindows, value))
                OnPlacementChanged();
        }
    }

    public bool Focusable
    {
        get => _focusable;
        set
        {
            if (Set(ref _focusable, value))
                Host?.Focus.SetPanel(value);
        }
    }

    internal override IWindowPlacer CreatePlacer(HostWindow host, ShellPlatform platform)
    {
        return _edge = new PanelEdge(new HostPanelDesktop(host, platform));
    }

    private protected override void ApplyPlacement()
    {
        _edge!.Set(new PanelPlacement(
            Screen,
            _anchors,
            _margins.Left,
            _margins.Top,
            _margins.Right,
            _margins.Bottom,
            double.IsNaN(Width) ? 100 : Width,
            double.IsNaN(Height) ? 100 : Height,
            _coversScreen,
            _reservedSize,
            _aboveWindows));
    }

    private protected override void ApplyFocus()
    {
        Host!.Focus.SetPanel(_focusable);
    }
}
