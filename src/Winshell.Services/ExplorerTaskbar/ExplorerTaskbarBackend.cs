using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell.ExplorerTaskbar;

/// <summary>
/// Hides Explorer's taskbar windows and removes the app-bar reservation that insets the work area, then puts both
/// back. An auto-hidden taskbar already reserves nothing, so only its window is hidden: removing that app bar and
/// registering it again would make it reserve an edge.
/// </summary>
internal sealed class ExplorerTaskbarBackend : IExplorerTaskbarBackend
{
    private readonly Lock _gate = new();
    private readonly Log _log;
    private readonly List<ExplorerTaskbarWindow.Saved> _saved = [];
    private bool _active;
    private bool _disposed;
    private IDisposable? _watch;

    /// <summary>Starts hearing Explorer make taskbar windows, on <paramref name="loop"/>'s thread.</summary>
    internal ExplorerTaskbarBackend(IEventLoop loop, Log log)
    {
        _log = log;
        // The watch gets its messages on the thread that makes it.
        loop.Post(() =>
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _watch = TaskbarWindows.Watch(() => TaskbarMayHaveChanged?.Invoke(),
                    e => log.Warn("explorer-taskbar", e.Message));
                if (_watch is null)
                    log.Warn("explorer-taskbar",
                        "Could not watch for a new Explorer taskbar; one Explorer makes stays shown");
            }
        });
    }

    public event Action? TaskbarMayHaveChanged;

    /// <summary>Takes every taskbar window not yet taken, so calling it again picks up one Explorer made since.</summary>
    public void Hide()
    {
        _active = true;
        if (_saved.Count == 0)
            _saved.AddRange(ExplorerTaskbarLease.Windows(ShellLocations.Current));
        _saved.RemoveAll(s => !ExplorerTaskbarWindow.IsExplorerTaskbar(s.Hwnd));
        var (windows, autohide) = ExplorerTaskbarWindow.Find();
        foreach (var (hwnd, primary) in windows)
        {
            var adopted = _saved.FirstOrDefault(saved => saved.Hwnd == hwnd);
            if (adopted is null)
                Take(hwnd, primary, autohide);
            else if (!PInvoke.IsWindowVisible((HWND)hwnd))
                KeepHidden(adopted);
            else
                TakeOver(adopted, hwnd, primary, autohide);
        }

        Remember();
    }

    public void Restore()
    {
        if (!_active && _saved.Count == 0)
            return;
        for (var i = _saved.Count - 1; i >= 0; i--)
            ExplorerTaskbarWindow.PutBack(_saved[i]);

        _saved.Clear();
        _active = false;
        ExplorerTaskbarLease.Forget(ShellLocations.Current);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _watch?.Dispose();
            _watch = null;
        }
    }

    private void Take(nint hwnd, bool primary, bool autohide)
    {
        if (!PInvoke.IsWindow((HWND)hwnd))
            return;
        var saved = ExplorerTaskbarWindow.Saved.Of(hwnd, primary, autohide, PInvoke.IsWindowVisible((HWND)hwnd));
        _saved.Add(saved);
        if (saved.Reserves)
            saved.Removed = ExplorerTaskbarWindow.Remove(hwnd);
        PInvoke.ShowWindow((HWND)hwnd, SHOW_WINDOW_CMD.SW_HIDE);
    }

    /// <summary>
    /// Hides a window the previous shell already hid, and keeps the visibility that shell recorded. Taking it again
    /// would remember the window as hidden, and putting it back would leave it hidden.
    /// </summary>
    private static void KeepHidden(ExplorerTaskbarWindow.Saved saved)
    {
        if (!PInvoke.IsWindow((HWND)saved.Hwnd))
            return;
        if (saved.Reserves && !saved.Removed)
            saved.Removed = ExplorerTaskbarWindow.Remove(saved.Hwnd);
        PInvoke.ShowWindow((HWND)saved.Hwnd, SHOW_WINDOW_CMD.SW_HIDE);
    }

    /// <summary>
    /// Hides a window the previous shell recorded that is visible again, and keeps that it was visible before any
    /// shell hid it.
    /// </summary>
    private void TakeOver(ExplorerTaskbarWindow.Saved adopted, nint hwnd, bool primary, bool autohide)
    {
        var wasVisible = adopted.WasVisible;
        _saved.Remove(adopted);
        Take(hwnd, primary, autohide);
        var taken = _saved.LastOrDefault(saved => saved.Hwnd == hwnd);
        if (taken is not null)
            taken.WasVisible = wasVisible || taken.WasVisible;
    }

    private void Remember()
    {
        var locations = ShellLocations.Current;
        if (_saved.Count == 0)
        {
            ExplorerTaskbarLease.Forget(locations);
            return;
        }

        if (!ExplorerTaskbarLease.Write(locations, Environment.ProcessId, _saved))
            _log.Warn("explorer-taskbar",
                "Could not record the hidden taskbar, so the next shell may show it in between.");
    }
}
