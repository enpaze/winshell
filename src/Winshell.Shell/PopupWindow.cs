namespace Winshell;

public partial class PopupWindow : ShellWindow
{
    private readonly Shell _shell;
    private PopupPosition _position = PopupPosition.Center;
    private string? _shownOn;
    private bool _grabFocus;
    private PopupPlacer? _placer;

    public PopupWindow()
        : this(Shell.Current)
    {
    }

    internal PopupWindow(Shell shell)
        : base(shell, visible: false)
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
        _shownOn = ShownOn(!Host!.Place.IsVisible, _shownOn, _shell.WindowList.ActiveWindow?.Screens);
        _placer!.Set(new PopupPlace(Screen?.Id, _shownOn, _position, Width, Height));
    }

    internal static string? ShownOn(bool opening, string? shownOn, IReadOnlyList<ScreenInfo>? active)
    {
        if (!opening)
            return shownOn;
        return active is [var screen, ..] ? screen.Id : null;
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
