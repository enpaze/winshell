using Winshell.Apps;

namespace Winshell.Tray;

public enum BalloonIcon
{
    None = 0,

    Info = 1,

    Warning = 2,

    Error = 3,

    App = 4
}

public sealed class SystemTray : ObservableObject, IShellService
{
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly ITrayBackend _backend;
    private readonly TrayIcons _icons;
    private readonly TrayClicker _clicker;
    private readonly Dictionary<string, SystemTrayItem> _items = [];
    private bool _open;
    private bool _waiting;
    private nint _answered;
    private IDisposable? _timer;
    private IDisposable? _answer;
    private bool _disposed;

    internal SystemTray(IEventLoop loop, Log log, ITrayBackend? backend = null)
    {
        _loop = loop;
        _log = log;
        _backend = backend ?? new TrayBackend(loop, log, TrayNames.Session);
        _icons = new TrayIcons(_backend.ReadIcon, OwnerOf);
        _clicker = new TrayClicker(loop, _backend);
        _icons.Added += OnChanged;
        _icons.Changed += OnChanged;
        _icons.Removed += OnRemoved;
        _icons.Balloon += OnBalloon;
        _backend.TaskbarCreated += OnTaskbarCreated;
        _backend.ExplorerInFront += _backend.Raise;
        _backend.SettingsChanged += UseSettings;
        loop.Post(Start);
    }

    public ObservableList<SystemTrayItem> Items { get => Read(field); } = [];

    private void Start()
    {
        _timer = null;
        if (_disposed)
            return;
        if (!_backend.TryOwn())
        {
            if (!_waiting)
                _log.Info("tray",
                    "Another winshell instance is the notification area; this one takes over when it exits");
            _waiting = true;
            _timer = _loop.Schedule(TimeSpan.FromMilliseconds(500), Start);
            return;
        }

        _waiting = false;
        if (!_backend.Open(_icons.Apply))
        {
            _backend.Close();
            _log.Warn("tray", "Could not create the notification area window; the system tray stays empty");
            return;
        }

        _open = true;
        AppBarMessages.Opened(_backend, _backend.Lower, _backend.Raise);
        _answered = _backend.Explorer;
        UseSettings();
        KeepInFront();
        _backend.Broadcast();
    }

    private void UseSettings()
    {
        _icons.UseSettings(_backend.ReadSettings());
    }

    private void KeepInFront()
    {
        try
        {
            _backend.Raise();
            foreach (var icon in _icons.Icons.ToArray())
                if (_backend.ProcessOf(icon.Window) == 0)
                    _icons.Remove(icon.Key);
        }
        finally
        {
            if (_open)
                _timer = _loop.Schedule(TimeSpan.FromSeconds(30), KeepInFront);
        }
    }

    private void OnTaskbarCreated()
    {
        _backend.Raise();
        var explorer = _backend.Explorer;
        if (explorer == 0 || explorer == _answered)
            return;
        _answered = explorer;
        _log.Info("tray", "Explorer made a new taskbar; asking apps to add their icons again");
        _answer?.Dispose();
        _answer = _loop.Schedule(TimeSpan.FromSeconds(1), () =>
        {
            _answer = null;
            _backend.Broadcast();
        });
    }

    private (int ProcessId, string ProgramPath) OwnerOf(nint window)
    {
        return ((int)_backend.ProcessOf(window), _backend.ProgramOf(window));
    }

    private void OnChanged(TrayIcon icon)
    {
        if (_items.TryGetValue(icon.Key, out var item))
        {
            if (icon.Hidden)
                OnRemoved(icon);
            else
                item.Refresh();
            return;
        }

        if (icon.Hidden)
            return;
        item = new SystemTrayItem(_backend, _clicker, icon);
        _items[icon.Key] = item;
        Items.Add(item);
        _loop.Post(() =>
        {
            if (!_disposed)
                item.OnReady();
        });
    }

    private void OnRemoved(TrayIcon icon)
    {
        if (_items.Remove(icon.Key, out var item))
            Items.Remove(item);
    }

    private void OnBalloon(TrayIcon icon, string title, string message, uint flags)
    {
        if (!_items.TryGetValue(icon.Key, out var item))
            return;
        var kind = (int)(flags & 0xf);
        item.OnBalloon(title, message, kind <= (int)BalloonIcon.App ? (BalloonIcon)kind : BalloonIcon.None);
    }

    void IShellService.Stop()
    {
        if (_disposed)
            return;
        _disposed = true;
        _timer?.Dispose();
        _answer?.Dispose();
        _timer = _answer = null;
        _clicker.Dispose();
        AppBarMessages.Closed(_backend);
        _backend.Close();
        if (_open)
            _backend.Broadcast();
        _open = false;
        _backend.Dispose();
    }
}

public sealed class SystemTrayItem : ObservableObject
{
    private readonly ITrayBackend _backend;
    private readonly TrayClicker _clicker;
    private readonly TrayIcon _icon;
    private PixelImage? _image;
    private string _tooltip;
    private bool _overflow;

    internal SystemTrayItem(ITrayBackend backend, TrayClicker clicker, TrayIcon icon)
    {
        _backend = backend;
        _clicker = clicker;
        _icon = icon;
        _image = icon.Image;
        _tooltip = icon.Tip;
        _overflow = icon.Overflow;
        Executable = icon.ProgramPath;
        ProcessId = icon.ProcessId;
        var file = Executable.Length > 0 ? Path.GetFileNameWithoutExtension(Executable) : icon.Key;
        Id = Executable.Length > 0 ? AppIdentity.DesktopId(Executable) : icon.Key;
        AppName = Executable.Length > 0 && AppIdentity.ProgramDescription(Executable) is { } description
            ? description
            : file;
    }

    public event Action? Ready;

    public event Action<string, string, BalloonIcon>? Balloon;

    public string Id { get => Read(field); }

    public string AppName { get => Read(field); }

    public string Executable { get => Read(field); }

    public int ProcessId { get => Read(field); }

    public PixelImage? Icon => Read(_image);

    public string Tooltip => Read(_tooltip);

    public bool Overflow => Read(_overflow);

    public PixelImage? IconAt(int size)
    {
        if (Icon is not { } image)
            return null;
        return size > 0 && size != image.Width && _icon.Resource is { } resource
            ? _backend.DrawIcon(resource, size) ?? image
            : image;
    }

    public void Click()
    {
        _clicker.Click(_icon, TrayClick.Left, null);
    }

    public void DoubleClick()
    {
        _clicker.Click(_icon, TrayClick.Double, null);
    }

    public void MiddleClick()
    {
        _clicker.Click(_icon, TrayClick.Middle, null);
    }

    public void Display()
    {
        _clicker.Click(_icon, TrayClick.Menu, null);
    }

    public void Display(int x, int y)
    {
        _clicker.Click(_icon, TrayClick.Menu, (x, y));
    }

    internal void Refresh()
    {
        Set(ref _image, _icon.Image, nameof(Icon));
        Set(ref _tooltip, _icon.Tip, nameof(Tooltip));
        Set(ref _overflow, _icon.Overflow, nameof(Overflow));
    }

    internal void OnReady()
    {
        Ready?.Invoke();
    }

    internal void OnBalloon(string title, string message, BalloonIcon icon)
    {
        Balloon?.Invoke(title, message, icon);
    }
}
