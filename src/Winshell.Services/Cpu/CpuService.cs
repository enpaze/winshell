using System.ComponentModel;

namespace Winshell.Cpu;

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

    public double Usage => _usage;

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
        _last = now;
        if (last is { } before && now is { } after)
            SetUsage(CpuTimes.Usage(before, after));
    }

    private void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _last = null;
        _usage = 0;
    }

    private void SetUsage(double usage)
    {
        if (usage == _usage)
            return;
        _usage = usage;
        _listeners?.Invoke(this, new PropertyChangedEventArgs(nameof(Usage)));
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
    }
}
