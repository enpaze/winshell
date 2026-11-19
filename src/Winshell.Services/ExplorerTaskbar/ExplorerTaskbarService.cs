namespace Winshell.ExplorerTaskbar;

public sealed class ExplorerTaskbarService : ObservableObject, IDisposable
{
    private readonly Lock _gate = new();
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private IExplorerTaskbarBackend? _backend;
    private bool _hidden;
    private bool _yielded;

    internal ExplorerTaskbarService(IEventLoop loop, Log log, IExplorerTaskbarBackend? backend = null)
    {
        _loop = loop;
        _log = log;
        if (backend is not null)
            _backend = Listen(backend);
    }

    public bool Hidden
    {
        get
        {
            lock (_gate)
            {
                return _hidden;
            }
        }
        set
        {
            if (Hidden == value)
                return;
            if (value)
                Hide();
            else
                Restore();
            Changed();
        }
    }

    internal void Restore()
    {
        lock (_gate)
        {
            if (!_hidden || _yielded)
                return;
            _backend?.Restore();
            _hidden = false;
        }
    }

    internal void Yield()
    {
        lock (_gate)
            _yielded = true;
        ExplorerTaskbarLease.HandOff(ShellLocations.Current);
    }

    void IDisposable.Dispose()
    {
        Restore();
        lock (_gate)
        {
            _backend?.Dispose();
        }
    }

    private void Hide()
    {
        lock (_gate)
        {
            if (_hidden)
                return;
            var taskbar = _backend ??= Listen(new ExplorerTaskbarBackend(_loop, _log));
            _hidden = true;
            try
            {
                taskbar.Hide();
            }
            catch
            {
                try
                {
                    taskbar.Restore();
                    _hidden = false;
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    _log.Warn("explorer-taskbar",
                        $"Could not put the taskbar back after hiding it failed: {e.Message}");
                }

                throw;
            }
        }
    }

    private void Rehide()
    {
        lock (_gate)
        {
            if (_hidden)
                _backend?.Hide();
        }
    }

    private IExplorerTaskbarBackend Listen(IExplorerTaskbarBackend backend)
    {
        backend.TaskbarMayHaveChanged += Rehide;
        return backend;
    }
}
