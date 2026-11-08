using System.ComponentModel;

namespace Winshell.Cpu;

/// <summary>
/// How busy the processors are. Windows is read every 2 seconds while something listens to
/// <see cref="PropertyChanged"/>, and not at all while nothing does.
/// </summary>
public sealed class CpuService : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly IEventLoop _loop;
    private readonly ICpuBackend _backend;
    private PropertyChangedEventHandler? _listeners;
    private IDisposable? _timer;
    private CpuTimes? _last;
    private double _usage;
    private bool _disposed;

    internal CpuService(IEventLoop loop, ICpuBackend? backend = null)
    {
        _loop = loop;
        _backend = backend ?? new CpuBackend();
    }

    /// <summary>
    /// The share of the last 2 seconds the processors were busy, across all cores, from 0 to 100. 0 while nothing
    /// listens, and for the first 2 seconds after something starts to.
    /// </summary>
    [PublicAPI]
    public double Usage => _usage;

    /// <summary>
    /// Raised on the loop's thread when <see cref="Usage"/> changes. The first handler added starts reading Windows,
    /// and removing the last stops it. Add and remove handlers on the loop's thread.
    /// </summary>
    [PublicAPI]
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            var first = _listeners is null;
            _listeners += value;
            if (first && _listeners is not null && !_disposed)
                Sample();
        }
        remove
        {
            _listeners -= value;
            if (_listeners is null)
                Stop();
        }
    }

    private void Sample()
    {
        _timer = _loop.Schedule(Interval, Sample);
        var now = _backend.Read();
        var last = _last;
        // A failed read starts over from the next one, rather than measure across the gap. The reading is kept before
        // listeners hear of it, so one that stops listening, or throws, cannot leave it stale.
        _last = now;
        if (last is { } before && now is { } after)
            SetUsage(CpuTimes.Usage(before, after));
    }

    private void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _last = null;
        // Nothing hears the reset: nothing listens, or the service is disposed.
        _usage = 0;
    }

    private void SetUsage(double usage)
    {
        if (usage == _usage)
            return;
        _usage = usage;
        _listeners?.Invoke(this, new PropertyChangedEventArgs(nameof(Usage)));
    }

    /// <summary>Stops reading Windows, and a handler added later does not start it again.</summary>
    [PublicAPI]
    public void Dispose()
    {
        _disposed = true;
        Stop();
    }
}
