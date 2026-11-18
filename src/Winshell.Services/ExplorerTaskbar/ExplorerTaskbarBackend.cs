using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell.ExplorerTaskbar;

internal sealed class ExplorerTaskbarBackend : IExplorerTaskbarBackend
{
    private readonly Lock _gate = new();
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly List<ExplorerTaskbarWindow.Saved> _saved = [];
    private readonly List<IDisposable> _later = [];
    private uint? _appBarState;
    private bool _active;
    private bool _hiding;
    private bool _disposed;
    private bool _showsArmed;
    private IDisposable? _watch;
    private WinEventHook? _shows;

    internal ExplorerTaskbarBackend(IEventLoop loop, Log log)
    {
        _loop = loop;
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
        if (_hiding)
            return;
        _hiding = true;
        try
        {
            _active = true;
            var locations = ShellLocations.Current;
            if (_saved.Count == 0)
            {
                _saved.AddRange(ExplorerTaskbarLease.Windows(locations));
                _appBarState ??= ExplorerTaskbarLease.AppBarState(locations);
            }

            _saved.RemoveAll(s => !ExplorerTaskbarWindow.IsExplorerTaskbar(s.Hwnd));
            var (windows, autohide) = ExplorerTaskbarWindow.Find();
            foreach (var (hwnd, primary) in windows)
            {
                var adopted = _saved.FirstOrDefault(saved => saved.Hwnd == hwnd);
                if (adopted is null)
                    Remember(hwnd, primary, autohide);
                else if (PInvoke.IsWindowVisible((HWND)hwnd))
                    TakeOver(adopted, hwnd, primary, autohide);
            }

            _appBarState ??= AppBarMessages.State();
            var recorded = RecordLease();
            var state = _appBarState.Value;
            if (recorded && (state & PInvoke.ABS_AUTOHIDE) == 0)
                AppBarMessages.SetState(state | PInvoke.ABS_AUTOHIDE);
            ExplorerTaskbarWindow.Conceal();
            Arm();
        }
        finally
        {
            _hiding = false;
        }
    }

    public void Restore()
    {
        if (!_active && _saved.Count == 0 && _appBarState is null)
            return;
        _active = false;
        Disarm();
        var state = _appBarState;
        _appBarState = null;
        if (state is uint saved)
            AppBarMessages.SetState(saved);
        for (var i = _saved.Count - 1; i >= 0; i--)
            ExplorerTaskbarWindow.PutBack(_saved[i]);

        _saved.Clear();
        ExplorerTaskbarLease.Forget(ShellLocations.Current);
    }

    public void Dispose()
    {
        Disarm();
        lock (_gate)
        {
            _disposed = true;
            _watch?.Dispose();
            _watch = null;
        }
    }

    private void Remember(nint hwnd, bool primary, bool autohide)
    {
        if (!PInvoke.IsWindow((HWND)hwnd))
            return;
        _saved.Add(ExplorerTaskbarWindow.Saved.Of(hwnd, primary, autohide, PInvoke.IsWindowVisible((HWND)hwnd)));
    }

    private void Arm()
    {
        if (_showsArmed)
            return;
        _showsArmed = true;
        _loop.Post(StartWatch);
        _later.Add(_loop.Schedule(TimeSpan.FromMilliseconds(400), ConcealIfActive));
        _later.Add(_loop.Schedule(TimeSpan.FromMilliseconds(1200), ConcealIfActive));
    }

    private void StartWatch()
    {
        if (!_active || _shows is not null)
            return;
        _shows = WinEventHook.Start(PInvoke.EVENT_OBJECT_SHOW, OnShown,
            e => _log.Warn("explorer-taskbar", e.Message));
        if (_shows is null)
            _log.Warn("explorer-taskbar", "Could not watch for Explorer showing its taskbar; it may come back");
    }

    private void OnShown(uint _, nint hwnd, int idObject, int idChild)
    {
        if (idObject != 0 || idChild != 0 || !_active || !ExplorerTaskbarWindow.IsForeignExplorerTaskbar(hwnd))
            return;
        _loop.Post(ConcealIfActive);
    }

    private void ConcealIfActive()
    {
        if (_active)
            ExplorerTaskbarWindow.Conceal();
    }

    private void Disarm()
    {
        foreach (var later in _later)
            later.Dispose();
        _later.Clear();
        _shows?.Dispose();
        _shows = null;
        _showsArmed = false;
    }

    private void TakeOver(ExplorerTaskbarWindow.Saved adopted, nint hwnd, bool primary, bool autohide)
    {
        var wasVisible = adopted.WasVisible;
        _saved.Remove(adopted);
        Remember(hwnd, primary, autohide);
        var taken = _saved.LastOrDefault(saved => saved.Hwnd == hwnd);
        if (taken is not null)
            taken.WasVisible = wasVisible || taken.WasVisible;
    }

    private bool RecordLease()
    {
        var locations = ShellLocations.Current;
        if (_saved.Count == 0)
        {
            ExplorerTaskbarLease.Forget(locations);
            return false;
        }

        if (ExplorerTaskbarLease.Write(locations, Environment.ProcessId, _saved, _appBarState))
            return true;
        _log.Warn("explorer-taskbar",
            "Could not record the hidden taskbar, so the next shell may show it in between.");
        return false;
    }
}
