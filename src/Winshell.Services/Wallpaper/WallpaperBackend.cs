using Winshell.Native;

namespace Winshell.Wallpaper;

/// <summary>One screen as Windows reported it, with the id a set uses.</summary>
internal readonly record struct WallpaperMonitor(string Id, string MonitorId, string Image, bool IsPrimary);

/// <summary>A reading of the desktop wallpaper.</summary>
internal sealed record WallpaperSnapshot(
    bool Available,
    IReadOnlyList<WallpaperMonitor> Monitors,
    WallpaperFit Fit,
    WallpaperColor Background)
{
    /// <summary>Explorer's wallpaper server did not answer.</summary>
    public static readonly WallpaperSnapshot Unavailable = new(false, [], WallpaperFit.Fill, default);
}

internal interface IWallpaperBackend : IDisposable
{
    /// <summary>Windows wrote the wallpaper. Raised on any thread, and possibly several times for one change.</summary>
    event Action? Changed;

    /// <summary>Reads each screen's image, the fit and the background color.</summary>
    WallpaperSnapshot Read();

    /// <summary>Sets one screen's image, or every screen's when <paramref name="monitorId"/> is null.</summary>
    bool SetImage(string? monitorId, string image);

    /// <summary>Sets the fit every screen uses.</summary>
    bool SetFit(WallpaperFit fit);

    /// <summary>Sets the background color every screen uses.</summary>
    bool SetBackground(WallpaperColor color);
}

/// <summary>
/// The desktop wallpaper Windows draws. A reading and a set go through <c>IDesktopWallpaper</c>. Windows 11 Settings
/// does not broadcast <c>WM_SETTINGCHANGE</c> for a wallpaper change, so a watch on the keys Settings writes raises
/// <see cref="Changed"/>: <c>Control Panel\Desktop</c>, <c>Control Panel\Colors</c> and
/// <c>Explorer\Wallpapers</c>.
/// </summary>
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

    /// <summary>
    /// <see cref="WallpaperFit"/> uses the same numbers as <c>DESKTOP_WALLPAPER_POSITION</c>. Fill when Windows
    /// names no position this service knows. Fill is the usual desktop fit.
    /// </summary>
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
