using Windows.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell;

internal sealed record PopupPlacement(ScreenInfo? Screen, Rect Rect);

internal sealed class PopupPlacer(HostWindow host, ShellPlatform platform) : IWindowPlacer
{
    private PopupPlacement? _placement;

    internal void Set(PopupPlacement placement)
    {
        _placement = placement;
        var monitors = Screens.List();
        var at = DesktopSpace.MonitorAt(new Point(placement.Rect.X, placement.Rect.Y),
            monitors.Select(m => m.Logical).ToList());
        var monitor = monitors.FirstOrDefault(m => m.Id == placement.Screen?.Id) ??
                      (at is { } i ? monitors[i] : Screens.Primary());
        if (monitor is null)
            return;
        host.Placement.Scale = monitor.Scale;
        var r = DesktopSpace.ToPhysical(placement.Rect, monitor.Bounds, monitor.Scale);
        host.Placement.Move(r, HWND.HWND_TOPMOST,
            (SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER));
    }

    public void OnDpiChanged(RECT suggested, bool placing)
    {
        if (!placing)
            platform.Loop.Post(Replace);
    }

    private void Replace()
    {
        if (host.Handle != 0 && _placement is { } placement)
            Set(placement);
    }
}
