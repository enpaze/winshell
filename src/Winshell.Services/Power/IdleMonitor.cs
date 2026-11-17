namespace Winshell.Power;

public sealed class IdleMonitor : ObservableObject, IDisposable
{
    private readonly IEventLoop _loop;
    private readonly IIdleBackend _idle;
    private readonly Func<bool> _keepAwakeActive;
    private readonly Log _log;
    private bool _enabled = true;
    private double _timeout;
    private bool _respectKeepAwake = true;
    private bool _isIdle;
    private bool _disposed;
    private IDisposable? _timer;

    internal IdleMonitor(IEventLoop loop, Log log, Func<bool> keepAwakeActive, IIdleBackend? idle = null)
    {
        _loop = loop;
        _log = log;
        _keepAwakeActive = keepAwakeActive;
        _idle = idle ?? new IdleBackend();
    }

    public bool Enabled
    {
        get => Read(_enabled);
        set
        {
            if (Set(ref _enabled, value))
                Check();
        }
    }

    public double Timeout
    {
        get => Read(_timeout);
        set
        {
            if (Set(ref _timeout, value))
                Check();
        }
    }

    public bool RespectKeepAwake
    {
        get => Read(_respectKeepAwake);
        set => Set(ref _respectKeepAwake, value);
    }

    public bool IsIdle => Read(_isIdle);

    private void Check()
    {
        _timer?.Dispose();
        _timer = null;
        if (_disposed)
            return;
        if (!_enabled || _timeout <= 0)
        {
            Set(ref _isIdle, false, nameof(IsIdle));
            return;
        }

        var next = TimeSpan.FromSeconds(1);
        try
        {
            var seconds = _idle.SecondsSinceLastUse();
            var idle = seconds >= _timeout && !(_respectKeepAwake && _keepAwakeActive());
            Set(ref _isIdle, idle, nameof(IsIdle));
            next = NextCheck(idle, _timeout - seconds);
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            _log.Warn("power", e.Message);
        }

        _timer = _loop.Schedule(next, Check);
    }

    internal static TimeSpan NextCheck(bool idle, double remaining)
    {
        return !idle && remaining > 0 ? TimeSpan.FromSeconds(Math.Clamp(remaining, 0.25, 5)) : TimeSpan.FromSeconds(1);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
