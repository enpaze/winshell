using System.ComponentModel;

namespace Winshell.Cpu;

public sealed class CpuService : INotifyPropertyChanged, IShellService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly IEventLoop _loop;
    private readonly ICpuBackend _backend;
    private readonly PropertyListeners _listeners;
    private IDisposable? _timer;
    private CpuTimes? _last;
    private double _usage;

    internal CpuService(IEventLoop loop, ICpuBackend? backend = null)
    {
        _loop = loop;
        _backend = backend ?? new CpuBackend();
        _listeners = new PropertyListeners(this, Sample, Stop);
    }

    public double Usage => _usage;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _listeners.Add(value);
        remove => _listeners.Remove(value);
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
        _listeners.Raise(nameof(Usage));
    }

    void IShellService.Stop()
    {
        _listeners.Dispose();
    }
}
