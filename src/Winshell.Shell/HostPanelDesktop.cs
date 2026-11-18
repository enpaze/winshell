using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell;

internal sealed class HostPanelDesktop(HostWindow host, ShellPlatform platform) : IPanelDesktop
{
    public PanelMonitor? Find(string id)
    {
        return Screens.Find(id) is { } monitor ? Of(monitor) : null;
    }

    public PanelMonitor? Primary()
    {
        return Screens.Primary() is { } monitor ? Of(monitor) : null;
    }

    public bool FullscreenOn(string screenId)
    {
        return platform.FullscreenOn(screenId);
    }

    public bool Register(uint callback)
    {
        return ExplorerTaskbarWindow.Register(host.Handle, callback);
    }

    public RECT Query(AppBarEdge edge, RECT proposed)
    {
        return ExplorerTaskbarWindow.Query(host.Handle, edge, proposed);
    }

    public RECT Reserve(AppBarEdge edge, RECT desired)
    {
        return ExplorerTaskbarWindow.Reserve(host.Handle, edge, desired);
    }

    public void Remove()
    {
        ExplorerTaskbarWindow.Remove(host.Handle);
    }

    public void Move(RECT rect, bool topmost, double scale)
    {
        host.Place.Scale = scale;
        host.Place.SetExStyle((uint)WINDOW_EX_STYLE.WS_EX_TOPMOST, topmost);
        var after = topmost ? HWND.HWND_TOPMOST : HWND.HWND_BOTTOM;
        host.Place.Move(rect, after,
            (SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER));
    }

    public void WorkAreaMayHaveChanged()
    {
        platform.WorkAreaMayHaveChanged();
    }

    public void Post(Action action)
    {
        platform.Loop.Post(action);
    }

    private static PanelMonitor Of(MonitorGeometry monitor)
    {
        return new PanelMonitor(monitor.Id, monitor.Bounds, monitor.Area, monitor.Scale);
    }
}
