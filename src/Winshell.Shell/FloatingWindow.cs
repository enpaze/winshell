using Windows.Foundation;

namespace Winshell;

public partial class FloatingWindow : ShellWindow
{
    private string _title = "";
    private Size _minimumSize;
    private Size _maximumSize;
    private bool _maximized;
    private bool _minimized;
    private bool _fullscreen;
    private bool _stateRequested;
    private FloatingWindow? _owner;
    private FloatingPlacer? _placer;

    public FloatingWindow()
        : this(Shell.Current)
    {
    }

    internal FloatingWindow(Shell shell)
        : base(shell)
    {
    }

    private protected override WindowKind Kind => WindowKind.Floating;

    public string Title
    {
        get => _title;
        set
        {
            if (Set(ref _title, value))
                OnPlaceChanged();
        }
    }

    public Size MinimumSize
    {
        get => _minimumSize;
        set
        {
            if (Set(ref _minimumSize, value))
                OnPlaceChanged();
        }
    }

    public Size MaximumSize
    {
        get => _maximumSize;
        set
        {
            if (Set(ref _maximumSize, value))
                OnPlaceChanged();
        }
    }

    public bool Maximized
    {
        get => _maximized;
        set
        {
            if (!Set(ref _maximized, value))
                return;
            if (value && _fullscreen)
            {
                _fullscreen = false;
                Changed(nameof(Fullscreen));
                _stateRequested = true;
            }

            OnPlaceChanged();
        }
    }

    public bool Minimized
    {
        get => _minimized;
        set
        {
            if (Set(ref _minimized, value))
                OnPlaceChanged();
        }
    }

    public bool Fullscreen
    {
        get => _fullscreen;
        set
        {
            if (Set(ref _fullscreen, value))
                OnPlaceChanged();
        }
    }

    public FloatingWindow? Owner
    {
        get => _owner;
        set
        {
            if (ReferenceEquals(_owner, value) || (value is not null && WouldOwn(value)))
                return;
            if (_owner is { } previous)
            {
                previous.HostCreated -= OnPlaceChanged;
                previous.Disposing -= OnOwnerDisposing;
            }

            _owner = value;
            if (value is not null)
            {
                value.HostCreated += OnPlaceChanged;
                value.Disposing += OnOwnerDisposing;
            }

            Changed();
            OnPlaceChanged();
        }
    }

    private void OnOwnerDisposing()
    {
        if (_owner is not { } owner)
            return;
        owner.HostCreated -= OnPlaceChanged;
        owner.Disposing -= OnOwnerDisposing;
        _owner = null;
        Changed(nameof(Owner));
    }

    private bool WouldOwn(FloatingWindow owner)
    {
        for (var hop = owner; hop is not null; hop = hop._owner)
            if (ReferenceEquals(hop, this))
                return true;
        return false;
    }

    internal override IWindowPlacer CreatePlacer(HostWindow host, ShellPlatform platform)
    {
        return _placer = new FloatingPlacer(host, platform, this);
    }

    private protected override void ApplyPlace()
    {
        _placer!.Set(new FloatingPlace(
            Screen,
            _title,
            double.IsNaN(Width) ? 640 : Width,
            double.IsNaN(Height) ? 480 : Height,
            _minimumSize,
            _maximumSize,
            _maximized,
            _minimized,
            _fullscreen,
            _owner?.Host,
            _stateRequested));
        _stateRequested = false;
    }

    internal void OnStateChanged(bool maximized, bool minimized)
    {
        Set(ref _maximized, maximized, nameof(Maximized));
        Set(ref _minimized, minimized, nameof(Minimized));
    }

    internal void OnFullscreenChanged(bool fullscreen)
    {
        Set(ref _fullscreen, fullscreen, nameof(Fullscreen));
    }
}
