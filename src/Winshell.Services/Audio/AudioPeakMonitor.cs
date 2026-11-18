using System.Runtime.InteropServices;

namespace Winshell.Audio;

public sealed class AudioPeakMonitor : ObservableObject, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(33);

    private readonly IEventLoop _loop;
    private AudioNode? _node;
    private bool _enabled = true;
    private double _peak;
    private double[] _peaks = [];
    private AudioChannel[] _channels = [];
    private bool _disposed;
    private IDisposable? _timer;
    private IDisposable? _keepRunning;
    private IAudioMeter? _meter;

    public AudioPeakMonitor(IEventLoop loop)
    {
        _loop = loop;
    }

    public AudioNode? Node
    {
        get => Read(_node);
        set
        {
            if (_node == value)
                return;
            _node?.Removed -= Restart;
            value?.Removed += Restart;
            Set(ref _node, value);
            Restart();
        }
    }

    public bool Enabled
    {
        get => Read(_enabled);
        set
        {
            if (Set(ref _enabled, value))
                Restart();
        }
    }

    public double Peak => Read(_peak);

    public IReadOnlyList<double> Peaks => Read(_peaks);

    public IReadOnlyList<AudioChannel> Channels => Read(_channels);

    private void Restart()
    {
        Stop();
        _meter = !_disposed && _enabled && _node is { IsRemoved: false } ? _node.Node.Meter : null;
        if (_meter is null)
        {
            Clear();
            return;
        }

        _keepRunning = _node!.Node.KeepRunning();
        Tick();
    }

    private void Tick()
    {
        _timer = null;
        if (_meter is not { } meter)
            return;
        try
        {
            var values = meter.ReadPeaks();
            if (values.Length > 0)
                Apply(values);
        }
        catch (COMException)
        {
        }

        if (ReferenceEquals(_meter, meter) && _timer is null)
            _timer = _loop.Schedule(Interval, Tick);
    }

    private void Apply(float[] values)
    {
        var peaks = values.Select(v => (double)v).ToArray();
        if (!peaks.SequenceEqual(_peaks))
        {
            _peaks = peaks;
            Changed(nameof(Peaks));
        }

        Set(ref _peak, values.Length == 0 ? 0 : values.Max(), nameof(Peak));
        var node = _node!.Node;
        var channels = node.Channels.Length == values.Length
            ? node.Channels
            : ChannelLayout.FromFormat(null, values.Length);
        if (!channels.SequenceEqual(_channels))
        {
            _channels = channels;
            Changed(nameof(Channels));
        }
    }

    private void Clear()
    {
        if (_peaks.Length > 0)
        {
            _peaks = [];
            Changed(nameof(Peaks));
        }

        if (_channels.Length > 0)
        {
            _channels = [];
            Changed(nameof(Channels));
        }

        Set(ref _peak, 0, nameof(Peak));
    }

    private void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _keepRunning?.Dispose();
        _keepRunning = null;
        _meter = null;
    }

    public void Dispose()
    {
        _disposed = true;
        Node = null;
    }
}
