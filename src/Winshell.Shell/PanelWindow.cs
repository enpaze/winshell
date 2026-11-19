using Microsoft.UI.Xaml;

namespace Winshell;

public partial class PanelWindow : ShellWindow
{
    private Edges _anchors;
    private Thickness _margins;
    private double _reservedSize;
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

    public Edges Anchors
    {
        get => _anchors;
        set
        {
            if (Set(ref _anchors, value))
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

    public double ReservedSize
    {
        get => _reservedSize;
        set
        {
            if (Set(ref _reservedSize, value))
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
        Host!.Focus.Arm(_focusable);
    }
}
