using Microsoft.UI.Xaml;

namespace Winshell;

public partial class PanelWindow : ShellWindow
{
    private Edge _edge = Edge.Top;
    private PanelAlign _align = PanelAlign.Center;
    private Thickness _margins;
    private bool _reserve;
    private bool _coversScreen;
    private bool _aboveWindows = true;
    private bool _focusable;
    private PanelPlacer? _placer;

    public PanelWindow()
        : this(Shell.Current)
    {
    }

    internal PanelWindow(Shell shell)
        : base(shell, visible: true)
    {
    }

    private protected override WindowKind Kind => WindowKind.Panel;

    public Edge Edge
    {
        get => _edge;
        set
        {
            if (Set(ref _edge, value))
                OnPlaceChanged();
        }
    }

    public PanelAlign Align
    {
        get => _align;
        set
        {
            if (Set(ref _align, value))
                OnPlaceChanged();
        }
    }

    public Thickness Margins
    {
        get => _margins;
        set
        {
            if (Set(ref _margins, value))
                OnPlaceChanged();
        }
    }

    public bool Reserve
    {
        get => _reserve;
        set
        {
            if (Set(ref _reserve, value))
                OnPlaceChanged();
        }
    }

    public bool CoversScreen
    {
        get => _coversScreen;
        set
        {
            if (Set(ref _coversScreen, value))
                OnPlaceChanged();
        }
    }

    public bool AboveWindows
    {
        get => _aboveWindows;
        set
        {
            if (Set(ref _aboveWindows, value))
                OnPlaceChanged();
        }
    }

    public bool Focusable
    {
        get => _focusable;
        set
        {
            if (Set(ref _focusable, value))
                Host?.Focus.Arm(value);
        }
    }

    internal override IWindowPlacer CreatePlacer(HostWindow host, ShellPlatform platform)
    {
        return _placer = new PanelPlacer(new HostPanelDesktop(host, platform));
    }

    private protected override void ApplyPlace()
    {
        _placer!.Set(new PanelPlace(
            Screen,
            _edge,
            _align,
            _margins.Left,
            _margins.Top,
            _margins.Right,
            _margins.Bottom,
            double.IsNaN(Width) ? 100 : Width,
            double.IsNaN(Height) ? 100 : Height,
            _coversScreen,
            _reserve,
            _aboveWindows));
    }

    private protected override void ApplyFocus()
    {
        Host!.Focus.Arm(_focusable);
    }
}
