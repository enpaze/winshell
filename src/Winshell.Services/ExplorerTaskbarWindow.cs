using System.Collections.Concurrent;
using System.Diagnostics;
using Winshell.Native;

namespace Winshell;

/// <summary>
/// Explorer's taskbar windows, and every app-bar message this process sends. Find, the saved state and putting a
/// window back serve the Explorer taskbar service and recovery. A shell's panels send their app-bar messages through
/// here too. A tray passes its own window's lower and raise when it opens: messages step that window behind
/// Explorer's taskbar and back, and this keeps no tray backend. When the first <c>Shell_TrayWnd</c> belongs to the
/// calling process, <c>SHAppBarMessage</c> passes the rectangle by an address in that process, which Explorer cannot
/// use once a tray passes the message on:
/// <c>ABM_QUERYPOS</c> and <c>ABM_SETPOS</c> then do nothing, and the work area stays as it was.
/// </summary>
internal static class ExplorerTaskbarWindow
{
    private static readonly ConcurrentDictionary<object, (Func<bool> Lower, Action Raise)> Trays = new();
    private static uint _callback;

    /// <summary>A tray of this process opened its window, which stands in front of Explorer's taskbar.</summary>
    /// <param name="tray">Which tray, so closing that one stops stepping around it.</param>
    /// <param name="lower">Puts the tray window behind Explorer's taskbar. False when it cannot.</param>
    /// <param name="raise">Puts the tray window back in front.</param>
    internal static void Opened(object tray, Func<bool> lower, Action raise)
    {
        Trays[tray] = (lower, raise);
    }

    /// <summary>The tray closed its window, so messages no longer step around it.</summary>
    internal static void Closed(object tray)
    {
        Trays.TryRemove(tray, out _);
    }

    /// <inheritdoc cref="AppBars.Register"/>
    internal static bool Register(nint window, uint callback)
    {
        return Send(() => AppBars.Register(window, callback));
    }

    /// <inheritdoc cref="AppBars.Query"/>
    internal static RECT Query(nint window, AppBarEdge edge, RECT proposed)
    {
        return Send(() => AppBars.Query(window, edge, proposed));
    }

    /// <inheritdoc cref="AppBars.Reserve"/>
    internal static RECT Reserve(nint window, AppBarEdge edge, RECT desired)
    {
        return Send(() => AppBars.Reserve(window, edge, desired));
    }

    /// <inheritdoc cref="AppBars.Remove"/>
    internal static bool Remove(nint window)
    {
        return Send(() => AppBars.Remove(window));
    }

    /// <inheritdoc cref="AppBars.Autohide"/>
    internal static bool Autohide(nint window)
    {
        return Send(() => AppBars.Autohide(window));
    }

    /// <inheritdoc cref="AppBars.TryPosition"/>
    internal static bool TryPosition(nint window, out AppBarEdge edge, out RECT bounds)
    {
        var (found, foundEdge, foundBounds) = Send(() =>
            (AppBars.TryPosition(window, out var e, out var b), e, b));
        edge = foundEdge;
        bounds = foundBounds;
        return found;
    }

    /// <summary>Explorer's taskbar windows, and whether its taskbar auto-hides.</summary>
    internal static (List<(nint Hwnd, bool Primary)> Windows, bool Autohide) Find()
    {
        var windows = Windows(TaskbarWindows.PrimaryClass).Select(hwnd => (Hwnd: hwnd, Primary: true))
            .Concat(Windows(TaskbarWindows.SecondaryClass).Select(hwnd => (Hwnd: hwnd, Primary: false))).ToList();
        return (windows, windows.Count > 0 && Autohide(windows[0].Hwnd));
    }

    /// <summary>Shows a window again and, when its app bar was removed, registers it and reserves its edge.</summary>
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
        // Reserve the free part of the saved edge. The edge is named again: the query is its own call.
        var offered = Query(saved.Hwnd, saved.Edge, saved.Rect);
        Reserve(saved.Hwnd, saved.Edge, offered);
    }

    /// <summary>
    /// The edge <paramref name="window"/> sits on, from where it is against <paramref name="monitor"/>. A window as
    /// wide as it is tall, or wider, sits on the top or the bottom; a taller one sits on the left or the right. The
    /// nearer edge wins, and a tie keeps the bottom or the right.
    /// </summary>
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

    /// <summary>False when the handle was destroyed or reused, so restore does not show some other window.</summary>
    internal static bool IsExplorerTaskbar(nint hwnd)
    {
        return PInvoke.IsWindow((HWND)hwnd) &&
               TopLevelWindows.ClassName(hwnd) is TaskbarWindows.PrimaryClass or TaskbarWindows.SecondaryClass;
    }

    /// <summary>A taskbar window as it was before it was hidden.</summary>
    internal sealed class Saved
    {
        public nint Hwnd;
        public bool WasVisible;

        /// <summary>Whether it reserved an edge: <see cref="Edge"/> and <see cref="Rect"/> say where.</summary>
        public bool Reserves;

        /// <summary>Whether its app bar was removed, so putting it back registers it again.</summary>
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

    /// <summary>Explorer's windows of that class. A shell's tray host makes a <c>Shell_TrayWnd</c> of its own.</summary>
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
            // It has exited.
            return false;
        }
    }
}
