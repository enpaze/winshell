using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell.ExplorerTaskbar;

internal sealed class ExplorerTaskbarBackend : IExplorerTaskbarBackend
{
    private readonly Lock _gate = new();
    private readonly Log _log;
    private readonly List<ExplorerTaskbarWindow.Saved> _saved = [];
    private bool _active;
    private bool _disposed;
    private IDisposable? _watch;

    internal ExplorerTaskbarBackend(IEventLoop loop, Log log)
    {
        _log = log;
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

        RecordLease();
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
            saved.Removed = AppBarMessages.Remove(hwnd);
        PInvoke.ShowWindow((HWND)hwnd, SHOW_WINDOW_CMD.SW_HIDE);
    }

    private static void KeepHidden(ExplorerTaskbarWindow.Saved saved)
    {
        if (!PInvoke.IsWindow((HWND)saved.Hwnd))
            return;
        if (saved.Reserves && !saved.Removed)
            saved.Removed = AppBarMessages.Remove(saved.Hwnd);
        PInvoke.ShowWindow((HWND)saved.Hwnd, SHOW_WINDOW_CMD.SW_HIDE);
    }

    private void TakeOver(ExplorerTaskbarWindow.Saved adopted, nint hwnd, bool primary, bool autohide)
    {
        var wasVisible = adopted.WasVisible;
        _saved.Remove(adopted);
        Take(hwnd, primary, autohide);
        var taken = _saved.LastOrDefault(saved => saved.Hwnd == hwnd);
        if (taken is not null)
            taken.WasVisible = wasVisible || taken.WasVisible;
    }

    private void RecordLease()
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
