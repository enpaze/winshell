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
    private readonly List<ExplorerTaskbarWindow.Saved> _saved = [];
    private bool _active;
    private bool _disposed;
    private IDisposable? _watch;

    /// <summary>Starts hearing Explorer make taskbar windows, on <paramref name="loop"/>'s thread.</summary>
    internal ExplorerTaskbarBackend(IEventLoop loop, Log log)
    {
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
        _saved.RemoveAll(s => !ExplorerTaskbarWindow.IsExplorerTaskbar(s.Hwnd));
        var (windows, autohide) = ExplorerTaskbarWindow.Find();
        foreach (var (hwnd, primary) in windows)
            if (_saved.All(s => s.Hwnd != hwnd))
                Take(hwnd, primary, autohide);
    }

    public void Restore()
    {
        if (!_active)
            return;
        for (var i = _saved.Count - 1; i >= 0; i--)
            ExplorerTaskbarWindow.PutBack(_saved[i]);

        _saved.Clear();
        _active = false;
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
}
