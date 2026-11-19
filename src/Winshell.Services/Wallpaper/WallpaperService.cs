namespace Winshell.Wallpaper;

public sealed class WallpaperService : ObservableObject, IShellService
{
    private readonly IWallpaperBackend _backend;
    private readonly SettleTimer _settle;
    private IReadOnlyList<WallpaperScreen> _screens = [];
    private WallpaperFit _fit = WallpaperFit.Fill;
    private WallpaperColor _background;
    private bool _known;
    private bool _disposed;

    internal WallpaperService(IEventLoop loop, Log log, IWallpaperBackend? backend = null)
    {
        _backend = backend ?? new WallpaperBackend(log);
        _settle = new SettleTimer(loop, () => Apply(_backend.Read()));
        _backend.Changed += _settle.Signal;
        Apply(_backend.Read());
    }

    public IReadOnlyList<WallpaperScreen> Screens => Read(_screens);

    public WallpaperFit Fit => Read(_fit);

    public WallpaperColor Background => Read(_background);

    public bool SetImage(WallpaperScreen screen, string image)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(image);
        if (_disposed || Find(screen) is not { } known)
            return false;
        if (string.IsNullOrWhiteSpace(image) || SamePath(known.Image, image))
            return false;
        if (!_backend.SetImage(known.MonitorId, image))
            return false;
        Apply(_backend.Read());
        return true;
    }

    public bool SetImage(string image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (_disposed || string.IsNullOrWhiteSpace(image))
            return false;
        if (_screens.Count > 0 && _screens.All(screen => SamePath(screen.Image, image)))
            return false;
        if (!_backend.SetImage(null, image))
            return false;
        Apply(_backend.Read());
        return true;
    }

    public bool SetFit(WallpaperFit fit)
    {
        if (_disposed || !Enum.IsDefined(fit) || (_known && fit == _fit))
            return false;
        if (!_backend.SetFit(fit))
            return false;
        Apply(_backend.Read());
        return true;
    }

    public bool SetBackground(WallpaperColor color)
    {
        if (_disposed || (_known && color == _background))
            return false;
        if (!_backend.SetBackground(color))
            return false;
        Apply(_backend.Read());
        return true;
    }

    void IShellService.Stop()
    {
        if (_disposed)
            return;
        _disposed = true;
        _settle.Dispose();
        _backend.Changed -= _settle.Signal;
        _backend.Dispose();
    }

    private void Apply(WallpaperSnapshot snapshot)
    {
        if (!snapshot.Available)
            return;
        _known = true;
        var screens = snapshot.Screens;
        var screensChanged = !Same(_screens, screens);
        var fitChanged = _fit != snapshot.Fit;
        var colorChanged = _background != snapshot.Background;
        _screens = screens;
        _fit = snapshot.Fit;
        _background = snapshot.Background;
        if (screensChanged)
            Changed(nameof(Screens));
        if (fitChanged)
            Changed(nameof(Fit));
        if (colorChanged)
            Changed(nameof(Background));
    }

    private WallpaperScreen? Find(WallpaperScreen screen)
    {
        if (screen.MonitorId.Length == 0)
            return null;
        foreach (var known in _screens)
            if (known.MonitorId == screen.MonitorId)
                return known;
        return null;
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Same(IReadOnlyList<WallpaperScreen> left, IReadOnlyList<WallpaperScreen> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var i = 0; i < left.Count; i++)
            if (left[i].Id != right[i].Id || left[i].MonitorId != right[i].MonitorId ||
                !SamePath(left[i].Image, right[i].Image) || left[i].IsPrimary != right[i].IsPrimary)
                return false;
        return true;
    }
}
