using Windows.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell;

internal sealed record PopupPlace(ScreenInfo? Screen, PopupPosition Position, double Width, double Height);

internal sealed class PopupPlacer(HostWindow host, ShellPlatform platform) : IWindowPlacer
{
    private PopupPlace? _place;

    internal void Set(PopupPlace place)
    {
        _place = place;
        var monitors = Screens.List();
        var at = place.Position.FollowsActiveWindow
            ? null
            : DesktopSpace.MonitorAt(place.Position.Origin, monitors.Select(m => m.Logical).ToList());
        var monitor = monitors.FirstOrDefault(m => m.Id == place.Screen?.Id) ??
                      (at is { } i ? monitors[i] : Screens.Primary());
        if (monitor is null)
            return;
        host.Place.Scale = monitor.Scale;
        var area = DesktopSpace.ToLogical(monitor.Area, monitor.Bounds, monitor.Scale);
        var r = DesktopSpace.ToPhysical(place.Position.Place(area, place.Width, place.Height), monitor.Bounds,
            monitor.Scale);
        host.Place.Move(r, HWND.HWND_TOPMOST,
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
