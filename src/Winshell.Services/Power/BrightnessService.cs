using System.Collections.Specialized;

namespace Winshell.Power;

public sealed class BrightnessMonitor : ObservableObject
{
    private readonly BrightnessWriter _writer;
    private readonly Log _log;
    private BrightnessInfo _info = new("", "", BrightnessConnection.External, 0, 100, 0);
    private int? _requested;

    internal BrightnessMonitor(BrightnessWriter writer, Log log)
    {
        _writer = writer;
        _log = log;
    }

    public string DeviceName => Read(_info).DeviceName;

    public string Name => Read(_info).Name;

    public BrightnessConnection Connection => Read(_info).Connection;

    public int Minimum => Read(_info).Minimum;

    public int Maximum => Read(_info).Maximum;

    public int Brightness
    {
        get => Read(_requested) ?? _info.Current;
        set
        {
            value = Math.Clamp(value, _info.Minimum, _info.Maximum);
            if (value == (_requested ?? _info.Current))
                return;
            _requested = value;
            Changed();
            var name = _info.Name;
            _writer.Request(_info.DeviceName, value, reason =>
                _log.Warn("power", $"Windows did not set the brightness of {name}: {reason}."));
        }
    }

    internal void Update(BrightnessInfo info)
    {
        var old = _info;
        var oldBrightness = _requested ?? old.Current;
        _info = info;
        if (!_writer.IsWriting(info.DeviceName))
            _requested = null;
        Changed(old.DeviceName != info.DeviceName, nameof(DeviceName));
        Changed(old.Name != info.Name, nameof(Name));
        Changed(old.Connection != info.Connection, nameof(Connection));
        Changed(old.Minimum != info.Minimum, nameof(Minimum));
        Changed(old.Maximum != info.Maximum, nameof(Maximum));
        Changed(oldBrightness != (_requested ?? info.Current), nameof(Brightness));
    }
}

public sealed class BrightnessService : ObservableObject, IDisposable
{
    private readonly IBrightnessBackend _backend;
    private readonly INotifyCollectionChanged _screens;
    private readonly StateReader<BrightnessSnapshot> _reader;
    private readonly BrightnessWriter _writer;
    private readonly Log _log;
    private bool _ready;

    internal BrightnessService(IEventLoop loop, Log log, INotifyCollectionChanged screens,
        IBrightnessBackend? backend = null)
    {
        _log = log;
        _screens = screens;
        _backend = backend ?? new BrightnessBackend(log);
        _reader = new StateReader<BrightnessSnapshot>(_backend.ReadAsync, Apply, loop,
            new FailureWarning(log, "power"));
        _writer = new BrightnessWriter(_backend.Set, log, _reader.Request);
        _backend.Changed += _reader.Request;
        _screens.CollectionChanged += OnScreensChanged;
        _reader.Request();
    }

    public ObservableList<BrightnessMonitor> Monitors { get => Read(field); } = [];

    public bool Ready => Read(_ready);

    private void Apply(BrightnessSnapshot snapshot)
    {
        Monitors.Sync(snapshot.Monitors, m => m.DeviceName, i => i.DeviceName,
            _ => new BrightnessMonitor(_writer, _log), (m, i) => m.Update(i));
        Set(ref _ready, true, nameof(Ready));
    }

    private void OnScreensChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var workAreaOnly = e is
        {
            Action: NotifyCollectionChangedAction.Replace,
            OldItems: [ScreenInfo old],
            NewItems: [ScreenInfo now]
        } && WorkAreaOnly(old, now);
        if (!workAreaOnly)
            _reader.Request();
    }

    private static bool WorkAreaOnly(ScreenInfo old, ScreenInfo now)
    {
        return old with { WorkArea = now.WorkArea } == now;
    }

    public void Dispose()
    {
        _screens.CollectionChanged -= OnScreensChanged;
        _backend.Changed -= _reader.Request;
        _reader.Stop(_backend.Dispose);
    }
}
