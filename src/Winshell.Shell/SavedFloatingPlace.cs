using Windows.Foundation;
using Winshell.Native;

namespace Winshell;

internal sealed record SavedFloatingPlace(
    uint Style,
    double Scale,
    RECT MonitorBounds,
    string MonitorId,
    double OffsetX,
    double OffsetY,
    double Width,
    double Height,
    bool Maximized,
    bool Minimized,
    WINDOWPLACEMENT Placement,
    bool HasPlacement)
{
    public bool Matches(string monitorId, RECT bounds, double scale)
    {
        return monitorId == MonitorId
               && bounds.left == MonitorBounds.left
               && bounds.top == MonitorBounds.top
               && bounds.right == MonitorBounds.right
               && bounds.bottom == MonitorBounds.bottom
               && Math.Abs(scale - Scale) < 0.001;
    }

    public RECT RectOn(RECT monitor, double scale)
    {
        var logical = new Rect(monitor.left + OffsetX, monitor.top + OffsetY, Math.Max(0, Width), Math.Max(0, Height));
        return DesktopSpace.ToPhysical(logical, monitor, scale);
    }
}
