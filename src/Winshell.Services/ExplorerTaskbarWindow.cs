using System.Collections.Concurrent;
using System.Diagnostics;
using Winshell.Native;

namespace Winshell;

internal static class ExplorerTaskbarWindow
{
    private static readonly ConcurrentDictionary<object, (Func<bool> Lower, Action Raise)> Trays = new();
    private static uint _callback;

    internal static void Opened(object tray, Func<bool> lower, Action raise)
    {
        Trays[tray] = (lower, raise);
    }

    internal static void Closed(object tray)
    {
        Trays.TryRemove(tray, out _);
    }

    internal static bool Register(nint window, uint callback)
    {
        return Send(() => AppBars.Register(window, callback));
    }

    internal static RECT Query(nint window, AppBarEdge edge, RECT proposed)
    {
        return Send(() => AppBars.Query(window, edge, proposed));
    }

    internal static RECT Reserve(nint window, AppBarEdge edge, RECT desired)
    {
        return Send(() => AppBars.Reserve(window, edge, desired));
    }

    internal static bool Remove(nint window)
    {
        return Send(() => AppBars.Remove(window));
    }

    internal static bool Autohide(nint window)
    {
        return Send(() => AppBars.Autohide(window));
    }

    internal static bool TryPosition(nint window, out AppBarEdge edge, out RECT bounds)
    {
        var (found, foundEdge, foundBounds) = Send(() =>
            (AppBars.TryPosition(window, out var e, out var b), e, b));
        edge = foundEdge;
        bounds = foundBounds;
        return found;
    }

    internal static (List<(nint Hwnd, bool Primary)> Windows, bool Autohide) Find()
    {
        var windows = Windows(TaskbarWindows.PrimaryClass).Select(hwnd => (Hwnd: hwnd, Primary: true))
            .Concat(Windows(TaskbarWindows.SecondaryClass).Select(hwnd => (Hwnd: hwnd, Primary: false))).ToList();
        return (windows, windows.Count > 0 && Autohide(windows[0].Hwnd));
    }

    internal static void PutBack(Saved saved)
    {
        if (!IsExplorerTaskbar(saved.Hwnd))
            return;
        if (saved.WasVisible)
            PInvoke.ShowWindow((HWND)saved.Hwnd, SHOW_WINDOW_CMD.SW_SHOWNA);
        if (!saved.Removed)
            return;
        if (!Register(saved.Hwnd, Callback()))
            return;
        var offered = Query(saved.Hwnd, saved.Edge, saved.Rect);
        Reserve(saved.Hwnd, saved.Edge, offered);
    }

    internal static AppBarEdge EdgeOf(RECT window, RECT monitor)
    {
        if (window.Height <= window.Width)
            return Math.Abs(window.top - monitor.top) < Math.Abs(monitor.bottom - window.bottom)
                ? AppBarEdge.Top
                : AppBarEdge.Bottom;
        return Math.Abs(window.left - monitor.left) < Math.Abs(monitor.right - window.right)
            ? AppBarEdge.Left
            : AppBarEdge.Right;
    }

    internal static bool IsExplorerTaskbar(nint hwnd)
    {
        return PInvoke.IsWindow((HWND)hwnd) &&
               TopLevelWindows.ClassName(hwnd) is TaskbarWindows.PrimaryClass or TaskbarWindows.SecondaryClass;
    }

    internal sealed class Saved
    {
        public nint Hwnd;
        public bool WasVisible;

        public bool Reserves;

        public bool Removed;

        public AppBarEdge Edge;
        public RECT Rect;

        internal static Saved Of(nint hwnd, bool primary, bool autohide, bool wasVisible)
        {
            var saved = new Saved { Hwnd = hwnd, WasVisible = wasVisible };
            saved.Reserves = !autohide && TryPlace(hwnd, primary, out saved.Edge, out saved.Rect);
            return saved;
        }
    }

    private static T Send<T>(Func<T> message)
    {
        List<object> lowered = [];
        foreach (var (tray, step) in Trays)
            if (step.Lower())
                lowered.Add(tray);
        try
        {
            return message();
        }
        finally
        {
            foreach (var tray in lowered)
                if (Trays.TryGetValue(tray, out var step))
                    step.Raise();
        }
    }

    private static bool TryPlace(nint hwnd, bool primary, out AppBarEdge edge, out RECT rect)
    {
        if (primary && TryPosition(hwnd, out edge, out rect))
            return true;

        if (PInvoke.GetWindowRect((HWND)hwnd, out rect) && rect is { Width: > 0, Height: > 0 })
        {
            edge = EdgeOf(rect, Screens.Of(hwnd)?.Bounds ?? rect);
            return true;
        }

        edge = AppBarEdge.Bottom;
        rect = default;
        return false;
    }

    private static uint Callback()
    {
        if (_callback == 0)
            _callback = PInvoke.RegisterWindowMessage("WinshellExplorerTaskbar");
        return _callback;
    }

    private static List<nint> Windows(string className)
    {
        var found = new List<nint>();
        for (nint hwnd = 0, next; (next = PInvoke.FindWindowEx(HWND.Null, (HWND)hwnd, className, null)) != 0; hwnd = next)
        {
            PInvoke.GetWindowThreadProcessId((HWND)next, out var pid);
            if (pid != Environment.ProcessId && IsExplorer((int)pid))
                found.Add(next);
        }

        return found;
    }

    private static bool IsExplorer(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return string.Equals(process.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
