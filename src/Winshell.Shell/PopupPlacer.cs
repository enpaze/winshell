using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell;

internal sealed record PopupPlace(string? Screen, string? Active, PopupPosition Position, double Width, double Height)
{
    internal MonitorGeometry? MonitorIn(IReadOnlyList<MonitorGeometry> monitors)
    {
        var id = Screen ?? (Position.FollowsActiveWindow ? Active : null);
        if (monitors.FirstOrDefault(m => m.Id == id) is { } chosen)
            return chosen;
        if (!Position.FollowsActiveWindow &&
            DesktopSpace.MonitorAt(Position.Origin, monitors.Select(m => m.Logical).ToList()) is { } at)
            return monitors[at];
        return monitors.Count > 0 ? monitors[0] : null;
    }

    internal RECT BoundsOn(MonitorGeometry monitor)
    {
        var area = DesktopSpace.ToLogical(monitor.Area, monitor.Bounds, monitor.Scale);
        var placed = Position.Place(area, double.IsNaN(Width) ? 100 : Width, double.IsNaN(Height) ? 100 : Height);
        return DesktopSpace.ToPhysical(placed, monitor.Bounds, monitor.Scale);
    }
}

internal sealed class PopupPlacer(HostWindow host, ShellPlatform platform) : IWindowPlacer
{
    private PopupPlace? _place;

    internal void Set(PopupPlace place)
    {
        _place = place;
        if (place.MonitorIn(Screens.List()) is not { } monitor)
            return;
        host.Place.Scale = monitor.Scale;
        host.Place.Move(place.BoundsOn(monitor), HWND.HWND_TOPMOST,
            (SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER));
    }

    public void OnDpiChanged(RECT suggested, bool placing)
    {
        if (!placing)
            platform.Loop.Post(Replace);
    }

    private void Replace()
    {
        if (host.Handle != 0 && _place is { } place)
            Set(place);
    }
}
