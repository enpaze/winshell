using Winshell.Native;

namespace Winshell.Wallpaper;

internal readonly record struct WallpaperMonitor(string Id, string MonitorId, string Image, bool IsPrimary);

internal sealed record WallpaperSnapshot(
    bool Available,
    IReadOnlyList<WallpaperMonitor> Monitors,
    WallpaperFit Fit,
    WallpaperColor Background)
{
    public static readonly WallpaperSnapshot Unavailable = new(false, [], WallpaperFit.Fill, default);
}

internal interface IWallpaperBackend : IDisposable
{
    event Action? Changed;

    WallpaperSnapshot Read();

    bool SetImage(string? monitorId, string image);

    bool SetFit(WallpaperFit fit);

    bool SetBackground(WallpaperColor color);
}

internal sealed class WallpaperBackend : IWallpaperBackend
{
    private static readonly string[] Keys =
    [
        @"Control Panel\Desktop",
        @"Control Panel\Colors",
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers"
    ];

    private readonly Log _log;
    private readonly RegistryWatch _watch;
    private int _loggedUnavailable;

    internal WallpaperBackend(Log log)
    {
        _log = log;
        _watch = new RegistryWatch(Keys, () => Changed?.Invoke());
    }

    public event Action? Changed;

    public WallpaperSnapshot Read()
    {
        var state = DesktopWallpaper.Read();
        if (!state.Available)
        {
            if (Interlocked.Exchange(ref _loggedUnavailable, 1) == 0)
                _log.Warn("wallpaper",
                    $"Explorer's desktop wallpaper is not available (0x{state.Error:X8}).");
            return WallpaperSnapshot.Unavailable;
        }

        Interlocked.Exchange(ref _loggedUnavailable, 0);
        var monitors = new WallpaperMonitor[state.Monitors.Count];
        for (var i = 0; i < monitors.Length; i++)
        {
            var monitor = state.Monitors[i];
            monitors[i] = new WallpaperMonitor(monitor.Id, monitor.MonitorId, monitor.Image, monitor.IsPrimary);
        }

        return new WallpaperSnapshot(true, monitors, Fit(state.Position), Color(state.Color));
    }

    public bool SetImage(string? monitorId, string image)
    {
        return Accepted(DesktopWallpaper.SetImage(monitorId, image));
    }

    public bool SetFit(WallpaperFit fit)
    {
        return Accepted(DesktopWallpaper.SetPosition((int)fit));
    }

    public bool SetBackground(WallpaperColor color)
    {
        return Accepted(DesktopWallpaper.SetColor((uint)(color.Red | (color.Green << 8) | (color.Blue << 16))));
    }

    public void Dispose()
    {
        _watch.Dispose();
    }

    private static WallpaperFit Fit(int position)
    {
        return position is >= (int)WallpaperFit.Center and <= (int)WallpaperFit.Span
            ? (WallpaperFit)position
            : WallpaperFit.Fill;
    }

    private static WallpaperColor Color(uint color)
    {
        return new WallpaperColor((byte)(color & 0xFF), (byte)((color >> 8) & 0xFF), (byte)((color >> 16) & 0xFF));
    }

    private bool Accepted(int hr)
    {
        if (hr >= 0)
            return true;
        _log.Warn("wallpaper", $"Windows did not set the wallpaper (0x{hr:X8}).");
        return false;
    }
}
