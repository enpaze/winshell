namespace Winshell.Audio;

public sealed class AudioService : ObservableObject, IDisposable
{
    private readonly IAudioBackend _backend;
    private readonly Log _log;
    private PlaybackDevice? _defaultPlayback;
    private RecordingDevice? _defaultRecording;

    internal AudioService(IEventLoop loop, Log log, IAudioBackend? backend = null)
    {
        _backend = backend ?? new AudioBackend(loop, log);
        _log = log;
        Refresh();
        OnDefaultsChanged();
        _backend.NodeAdded += OnNodeChanged;
        _backend.NodeRemoved += OnNodeChanged;
        _backend.DefaultsChanged += OnDefaultsChanged;
    }

    public ObservableList<PlaybackDevice> PlaybackDevices { get => Read(field); } = [];

    public ObservableList<RecordingDevice> RecordingDevices { get => Read(field); } = [];

    public PlaybackDevice? DefaultPlaybackDevice
    {
        get => Read(_defaultPlayback);
        set => SetDefault(value);
    }

    public RecordingDevice? DefaultRecordingDevice
    {
        get => Read(_defaultRecording);
        set => SetDefault(value);
    }

    public bool Ready => Read(_backend).Ready;

    private void SetDefault(AudioDevice? device)
    {
        if (device is not null)
            _backend.SetDefault(device.Node.Name);
    }

    private void OnNodeChanged(AudioGraphNode _)
    {
        Refresh();
    }

    private void Refresh()
    {
        Sync(PlaybackDevices, AudioNodeType.PlaybackDevice, node => new PlaybackDevice(node, _log));
        Sync(RecordingDevices, AudioNodeType.RecordingDevice, node => new RecordingDevice(node, _log));
    }

    private void Sync<T>(ObservableList<T> devices, AudioNodeType type, Func<AudioGraphNode, T> create)
        where T : AudioDevice
    {
        devices.Sync(_backend.Nodes.Where(n => n.Type == type), d => d.Node, n => n, create, static (_, _) => { },
            d => d.Remove());
        foreach (var device in devices)
            device.Sessions.Sync(_backend.Nodes.Where(n => n.Device == device.Node), s => s.Node, n => n,
                n => new AudioSession(device, n, _log), static (_, _) => { }, s => s.Remove());
    }

    private void OnDefaultsChanged()
    {
        Set(ref _defaultPlayback, Find(PlaybackDevices, _backend.DefaultPlayback), nameof(DefaultPlaybackDevice));
        Set(ref _defaultRecording, Find(RecordingDevices, _backend.DefaultRecording), nameof(DefaultRecordingDevice));
    }

    private static T? Find<T>(ObservableList<T> devices, AudioGraphNode? node)
        where T : AudioDevice
    {
        return devices.FirstOrDefault(d => d.Node == node);
    }

    public void Dispose()
    {
        _backend.NodeAdded -= OnNodeChanged;
        _backend.NodeRemoved -= OnNodeChanged;
        _backend.DefaultsChanged -= OnDefaultsChanged;
        foreach (var device in PlaybackDevices)
            device.Remove();
        foreach (var device in RecordingDevices)
            device.Remove();
        _backend.Dispose();
    }
}
