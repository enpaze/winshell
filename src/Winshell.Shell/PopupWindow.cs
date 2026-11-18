namespace Winshell;

public sealed partial class PopupWindow : ShellWindow
{
    private readonly Shell _shell;
    private PopupPosition _position = PopupPosition.Center;
    private ScreenInfo? _shownOn;
    private bool _grabFocus;
    private PopupPlacer? _placer;

    public PopupWindow()
        : this(Shell.Current)
    {
    }

    internal PopupWindow(Shell shell)
        : base(shell)
    {
        _shell = shell;
    }

    private protected override WindowKind Kind => WindowKind.Popup;

    public PopupPosition Position
    {
        get => _position;
        set
        {
            if (Set(ref _position, value))
                OnPlaceChanged();
        }
    }

    public bool GrabFocus
    {
        get => _grabFocus;
        set
        {
            if (Set(ref _grabFocus, value))
                Host?.Focus.Arm(value);
        }
    }

    public event Action? Dismissed;

    internal override IWindowPlacer CreatePlacer(HostWindow host, ShellPlatform platform)
    {
        return _placer = new PopupPlacer(host, platform);
    }

    private protected override void ApplyPlace()
    {
        if (!Host!.Place.IsVisible)
            _shownOn = _position.FollowsActiveWindow && _shell.WindowList.ActiveWindow?.Screens is [var active, ..]
                ? active
                : null;
        _placer!.Set(new PopupPlace(
            Screen ?? _shownOn,
            _position,
            double.IsNaN(Width) ? 100 : Width,
            double.IsNaN(Height) ? 100 : Height));
    }

    private protected override void ApplyFocus()
    {
        Host!.Focus.Arm(_grabFocus);
    }

    internal override void OnDismissed()
    {
        Visible = false;
        Dismissed?.Invoke();
    }
}
