using Windows.Foundation;

namespace Winshell;

public sealed partial class PopupWindow : ShellWindow
{
    private Rect _rect = new(0, 0, 100, 100);
    private bool _grabFocus;
    private PopupPlacer? _placer;

    public PopupWindow()
        : this(Shell.Current)
    {
    }

    internal PopupWindow(Shell shell)
        : base(shell)
    {
    }

    private protected override WindowKind Kind => WindowKind.Popup;

    public Rect Rect
    {
        get => _rect;
        set
        {
            if (Set(ref _rect, value))
                OnPlacementChanged();
        }
    }

    public bool GrabFocus
    {
        get => _grabFocus;
        set
        {
            if (Set(ref _grabFocus, value))
                Host?.Focus.SetPopup(value);
        }
    }

    public event Action? Dismissed;

    internal override IWindowPlacer CreatePlacer(HostWindow host, ShellPlatform platform)
    {
        return _placer = new PopupPlacer(host, platform);
    }

    private protected override void ApplyPlacement()
    {
        _placer!.Set(new PopupPlacement(Screen, _rect));
    }

    private protected override void ApplyFocus()
    {
        Host!.Focus.SetPopup(_grabFocus);
    }

    internal override void OnDismissed()
    {
        Visible = false;
        Dismissed?.Invoke();
    }
}
