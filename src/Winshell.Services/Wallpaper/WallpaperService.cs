namespace Winshell.Wallpaper;

/// <summary>
/// The desktop wallpaper Windows draws. Each screen has its own image. The fit and the background color are shared,
/// and a change stays after the shell exits, because it is Windows' own setting. A slideshow or Windows Spotlight
/// reports the image Windows is showing on that screen, or none when Windows reports no image. This does not manage
/// a slideshow.
/// Windows writes several values for one change and does not broadcast it, so a change made in Settings is read a
/// tenth of a second later and raised on the loop's thread. Until a reading succeeds there are no screens. A later
/// reading that cannot reach Explorer's desktop leaves the last one in place, and a set does nothing.
/// </summary>
public sealed class WallpaperService : ObservableObject, IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(100);

    private readonly IEventLoop _loop;
    private readonly IWallpaperBackend _backend;
    private IDisposable? _settle;
    private IReadOnlyList<WallpaperScreen> _screens = [];
    private WallpaperFit _fit = WallpaperFit.Fill;
    private WallpaperColor _background;
    private bool _known;
    private bool _disposed;

    internal WallpaperService(IEventLoop loop, Log log, IWallpaperBackend? backend = null)
    {
        _loop = loop;
        _backend = backend ?? new WallpaperBackend(log);
        _backend.Changed += OnChanged;
        Apply(_backend.Read());
    }

    /// <summary>
    /// Each screen's wallpaper, in the order Windows lists the monitors. Empty when the desktop wallpaper cannot
    /// be read.
    /// </summary>
    public IReadOnlyList<WallpaperScreen> Screens => Read(_screens);

    /// <summary>How the image is fitted. The same fit is used on every screen.</summary>
    public WallpaperFit Fit => Read(_fit);

    /// <summary>
    /// The color behind the image, and the color of a screen with no image. The same color is used on every screen.
    /// </summary>
    public WallpaperColor Background => Read(_background);

    /// <summary>
    /// Sets <paramref name="image"/> on <paramref name="screen"/> and leaves the others as they are. An unknown
    /// screen, an empty path and the image already there are ignored. Returns whether Windows was asked. Call it on
    /// the loop's thread. <paramref name="image"/> is the full path of an image file.
    /// </summary>
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

    /// <summary>
    /// Sets <paramref name="image"/> on every screen. An empty path, and an image every screen already shows, are
    /// ignored. Returns whether Windows was asked. Call it on the loop's thread. <paramref name="image"/> is the
    /// full path of an image file.
    /// </summary>
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

    /// <summary>
    /// Sets the fit every screen uses. The fit already in use, and one Windows does not define, are ignored. Until a
    /// reading succeeds, the fit shown is not one Windows reported, so setting it still asks Windows. Returns whether
    /// Windows was asked. Call it on the loop's thread.
    /// </summary>
    public bool SetFit(WallpaperFit fit)
    {
        if (_disposed || !Enum.IsDefined(fit) || (_known && fit == _fit))
            return false;
        if (!_backend.SetFit(fit))
            return false;
        Apply(_backend.Read());
        return true;
    }

    /// <summary>
    /// Sets the background color every screen uses. The color already in use is ignored. Until a reading succeeds, the
    /// color shown is not one Windows reported, so setting it still asks Windows. Returns whether Windows was asked.
    /// Call it on the loop's thread.
    /// </summary>
    public bool SetBackground(WallpaperColor color)
    {
        if (_disposed || (_known && color == _background))
            return false;
        if (!_backend.SetBackground(color))
            return false;
        Apply(_backend.Read());
        return true;
    }

    /// <summary>Stops watching the wallpaper, and a later set does nothing.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _settle?.Dispose();
        _settle = null;
        _backend.Changed -= OnChanged;
        _backend.Dispose();
    }

    private void OnChanged()
    {
        if (_disposed)
            return;
        _loop.Post(Schedule);
    }

    private void Schedule()
    {
        if (_disposed)
            return;
        _settle?.Dispose();
        _settle = _loop.Schedule(Settle, () =>
        {
            _settle = null;
            if (!_disposed)
                Apply(_backend.Read());
        });
    }

    private void Apply(WallpaperSnapshot snapshot)
    {
        if (!snapshot.Available)
            return;
        _known = true;
        var screens = new WallpaperScreen[snapshot.Monitors.Count];
        for (var i = 0; i < screens.Length; i++)
        {
            var monitor = snapshot.Monitors[i];
            screens[i] = new WallpaperScreen(monitor.Id, monitor.MonitorId, monitor.Image, monitor.IsPrimary);
        }

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
