using Winshell.Windows;

namespace Winshell.Capture;

public sealed record CaptureTarget(nint Window, string MonitorId)
{
    internal AppWindow? Source { get; private init; }

    public static CaptureTarget ForWindow(nint window)
    {
        return new CaptureTarget(window, "");
    }

    public static CaptureTarget ForWindow(AppWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new CaptureTarget(window.Hwnd, "") { Source = window };
    }

    public static implicit operator CaptureTarget(AppWindow window)
    {
        return ForWindow(window);
    }

    public static CaptureTarget ForScreen(ScreenInfo screen)
    {
        return new CaptureTarget(0, screen.Id);
    }
}
