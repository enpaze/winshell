using System.Diagnostics;
using Winshell.Native;

namespace Winshell.ExplorerTaskbar;

internal static class ExplorerTaskbarWindow
{
    private static uint _callback;

    internal static (List<(nint Hwnd, bool Primary)> Windows, bool Autohide) Find()
    {
        var windows = Windows(TaskbarWindows.PrimaryClass).Select(hwnd => (Hwnd: hwnd, Primary: true))
            .Concat(Windows(TaskbarWindows.SecondaryClass).Select(hwnd => (Hwnd: hwnd, Primary: false))).ToList();
        return (windows, windows.Count > 0 && AppBarMessages.Autohide(windows[0].Hwnd));
    }

    internal static bool AnyHidden()
    {
        return Find().Windows.Any(taskbar => !PInvoke.IsWindowVisible((HWND)taskbar.Hwnd));
    }

    internal static void RestoreHidden()
    {
        var (windows, autohide) = Find();
        foreach (var (hwnd, primary) in windows)
        {
            if (PInvoke.IsWindowVisible((HWND)hwnd))
                continue;
            var saved = Saved.Of(hwnd, primary, autohide, true);
            saved.Removed = saved.Reserves;
            PutBack(saved);
        }
    }

    internal static void PutBack(Saved saved)
    {
        if (!IsExplorerTaskbar(saved.Hwnd))
            return;
        if (saved.WasVisible)
            PInvoke.ShowWindow((HWND)saved.Hwnd, SHOW_WINDOW_CMD.SW_SHOWNA);
        if (!saved.Removed)
            return;
        if (!AppBarMessages.Register(saved.Hwnd, Callback()))
            return;
        var offered = AppBarMessages.Query(saved.Hwnd, saved.Edge, saved.Rect);
        AppBarMessages.Reserve(saved.Hwnd, saved.Edge, offered);
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

    internal static bool IsForeignExplorerTaskbar(nint hwnd)
    {
        if (!IsExplorerTaskbar(hwnd))
            return false;
        PInvoke.GetWindowThreadProcessId((HWND)hwnd, out var pid);
        return pid != Environment.ProcessId && IsExplorer((int)pid);
    }

    internal static void Conceal()
    {
        foreach (var (hwnd, _) in Find().Windows)
            PInvoke.ShowWindow((HWND)hwnd, SHOW_WINDOW_CMD.SW_HIDE);
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

    private static bool TryPlace(nint hwnd, bool primary, out AppBarEdge edge, out RECT rect)
    {
        if (primary && AppBarMessages.TryPosition(hwnd, out edge, out rect))
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
