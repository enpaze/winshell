using Winshell.Native;

namespace Winshell;

internal readonly record struct MonitorFrame(int Left, int Top, int Right, int Bottom, double Scale);

internal readonly record struct WindowToMove(
    RECT Shown,
    RECT Restored,
    bool HasPlacement,
    bool Minimized,
    bool Maximized,
    bool ToolWindow);

internal readonly record struct WindowPlace(RECT Screen, RECT Workspace);

internal static class WindowOnScreen
{
    public static RECT Restored(WindowToMove window, RECT primaryMonitor, RECT primaryWork)
    {
        var bias = Bias(window, primaryMonitor, primaryWork);
        return RectangleToMove(window.Minimized, window.Maximized, window.Shown,
            Shift(window.Restored, bias.X, bias.Y));
    }

    public static bool TryMove(WindowToMove window, MonitorFrame from, MonitorFrame to, RECT primaryMonitor,
        RECT primaryWork, out WindowPlace place)
    {
        var source = Restored(window, primaryMonitor, primaryWork);
        if (source is not { Width: > 0, Height: > 0 })
        {
            place = default;
            return false;
        }

        var screen = MoveOnto(source, from, to);
        var bias = Bias(window, primaryMonitor, primaryWork);
        place = new WindowPlace(screen, window.HasPlacement ? Shift(screen, -bias.X, -bias.Y) : screen);
        return true;
    }

    private static (int X, int Y) Bias(WindowToMove window, RECT primaryMonitor, RECT primaryWork)
    {
        if (window is { HasPlacement: true, Minimized: false, Maximized: false })
            return (window.Shown.left - window.Restored.left, window.Shown.top - window.Restored.top);
        if (window.HasPlacement)
            return WorkspaceOffset(primaryMonitor, primaryWork, window.ToolWindow);
        return (0, 0);
    }

    private static RECT RectangleToMove(bool minimized, bool maximized, RECT current, RECT restored)
    {
        if ((minimized || maximized) && restored is { Width: > 0, Height: > 0 })
            return restored;
        return current;
    }

    private static (int X, int Y) WorkspaceOffset(RECT primaryMonitor, RECT primaryWork, bool toolWindow)
    {
        return toolWindow || primaryWork is not { Width: > 0, Height: > 0 }
            ? (0, 0)
            : (primaryWork.left - primaryMonitor.left, primaryWork.top - primaryMonitor.top);
    }

    private static RECT Shift(RECT rect, int x, int y)
    {
        return new RECT(rect.left + x, rect.top + y, rect.right + x, rect.bottom + y);
    }

    private static RECT MoveOnto(RECT window, MonitorFrame from, MonitorFrame to)
    {
        if (from.Scale <= 0 || to.Scale <= 0 || from.Right <= from.Left || from.Bottom <= from.Top)
            return window;
        var width = (window.right - window.left) / from.Scale * to.Scale;
        var height = (window.bottom - window.top) / from.Scale * to.Scale;
        var left = to.Left + (window.left - from.Left) / from.Scale * to.Scale;
        var top = to.Top + (window.top - from.Top) / from.Scale * to.Scale;
        var targetWidth = to.Right - to.Left;
        var targetHeight = to.Bottom - to.Top;
        if (width > targetWidth)
            width = targetWidth;
        if (height > targetHeight)
            height = targetHeight;
        if (width < 1)
            width = 1;
        if (height < 1)
            height = 1;
        if (left < to.Left)
            left = to.Left;
        if (top < to.Top)
            top = to.Top;
        if (left + width > to.Right)
            left = to.Right - width;
        if (top + height > to.Bottom)
            top = to.Bottom - height;
        var x = (int)Math.Round(left);
        var y = (int)Math.Round(top);
        var w = Math.Max(1, (int)Math.Round(width));
        var h = Math.Max(1, (int)Math.Round(height));
        return new RECT(x, y, x + w, y + h);
    }
}
