namespace Winshell.Power;

/// <summary>Tells when the person has not used the PC for a while. Input this process sent does not count.</summary>
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

    /// <param name="loop">The loop the idle check is scheduled on.</param>
    /// <param name="log">Where a failed idle read is reported.</param>
    /// <param name="keepAwakeActive">
    /// Whether a <see cref="KeepAwake"/> is holding the PC awake. The shell passes the count that service holds.
    /// </param>
    /// <param name="idle">Where the seconds come from. Omitted, this reads Windows.</param>
    internal IdleMonitor(IEventLoop loop, Log log, Func<bool> keepAwakeActive, IIdleBackend? idle = null)
    {
        _loop = loop;
        _log = log;
        _keepAwakeActive = keepAwakeActive;
        _idle = idle ?? new IdleBackend();
    }

    /// <summary>
    /// Whether the monitor watches for idle. While false, <see cref="IsIdle"/> is false. True by default.
    /// </summary>
    public bool Enabled
    {
        get => Read(_enabled);
        set
        {
            if (Set(ref _enabled, value))
                Check();
        }
    }

    /// <summary>The seconds without the person's input after which <see cref="IsIdle"/> turns on.</summary>
    public double Timeout
    {
        get => Read(_timeout);
        set
        {
            if (Set(ref _timeout, value))
                Check();
        }
    }

    /// <summary>When a <see cref="KeepAwake"/> is on, this monitor does not report idle.</summary>
    public bool RespectKeepAwake
    {
        get => Read(_respectKeepAwake);
        set => Set(ref _respectKeepAwake, value);
    }

    /// <summary>
    /// Whether the person has not used the PC for <see cref="Timeout"/> seconds. False while <see cref="Timeout"/> is 0.
    /// </summary>
    public bool IsIdle => Read(_isIdle);

    private void Check()
    {
        _timer?.Dispose();
        _timer = null;
        if (_disposed)
            return;
        // A change to Enabled or Timeout checks again, so a monitor that cannot be idle does not poll.
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
#pragma warning disable CA1031 // A failed idle read must not stop the poll.
        catch (Exception e)
#pragma warning restore CA1031
        {
            _log.Warn("power", e.Message);
        }

        _timer = _loop.Schedule(next, Check);
    }

    /// <summary>
    /// When to look again. Before the timeout, at the moment it can pass, at most 5 s ahead. Once idle, or held
    /// awake past it, once a second.
    /// </summary>
    internal static TimeSpan NextCheck(bool idle, double remaining)
    {
        return !idle && remaining > 0 ? TimeSpan.FromSeconds(Math.Clamp(remaining, 0.25, 5)) : TimeSpan.FromSeconds(1);
    }

    /// <summary>Stops checking for idle.</summary>
    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
