using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace Winshell.Audio;

public sealed class AudioNode : ObservableObject
{
    private readonly AudioGraphNode _node;
    private readonly Log _log;
    private string _description;
    private IReadOnlyList<float> _volumes;
    private bool _muted;
    private bool _active;

    internal AudioNode(AudioService service, AudioGraphNode node, Log log)
    {
        Service = service;
        _node = node;
        _log = log;
        _description = node.Description;
        _volumes = node.Volumes;
        _muted = node.Muted;
        _active = node.Active;
        Channels = node.Channels;
        node.PropertiesChanged += OnPropertiesChanged;
        node.AudioChanged += OnAudioChanged;
        node.ActiveChanged += OnActiveChanged;
    }

    private AudioService Service { get; }

    internal AudioGraphNode Node => _node;

    internal bool IsRemoved { get; private set; }

    internal event Action? Removed;

    public int Id => Read(_node).Id;

    public string Name => Read(_node).Name;

    public string Description => Read(_description);

    public string InterfaceName => Read(_node).InterfaceName;

    public bool IsPlayback => Read(_node).IsPlayback;

    public bool IsSession => Read(_node).IsSession;

    public AudioNode? Device => Service.Find(Read(_node).Device);

    public bool Active => Read(_active);

    public IReadOnlyDictionary<string, string> Properties => Read(_node).Properties;

    public bool Muted
    {
        get => Read(_muted);
        set => Apply(() => _node.SetMuted(value));
    }

    public double Volume
    {
        get => Average(Read(_volumes));
        set
        {
            var old = Average(_volumes);
            if (old == 0)
                SetLevels(Enumerable.Repeat(value, _volumes.Count).ToArray());
            else
                SetLevels([.. _volumes.Select(level => level * (value / old))]);
        }
    }

    public IReadOnlyList<double> Volumes
    {
        get => [.. Read(_volumes).Select(v => (double)v)];
        set => SetLevels(value);
    }

    public IReadOnlyList<AudioChannel> Channels { get => Read(field); }

    internal void Remove()
    {
        if (IsRemoved)
            return;
        IsRemoved = true;
        _node.PropertiesChanged -= OnPropertiesChanged;
        _node.AudioChanged -= OnAudioChanged;
        _node.ActiveChanged -= OnActiveChanged;
        Removed?.Invoke();
    }

    private static double Average(IReadOnlyList<float> volumes)
    {
        return volumes.Count == 0 ? 0 : volumes.Average();
    }

    private void SetLevels(IReadOnlyList<double>? levels)
    {
        var count = _volumes.Count;
        if (count == 0)
            return;
        var requested = levels ?? [];
        if (requested.Count == 0)
            return;

        float[] next = requested.Count == count
            ? [.. requested.Select(level => (float)Math.Max(0, level))]
            : [.. Enumerable.Repeat((float)Math.Max(0, requested[0]), count)];
        Apply(() => _node.SetVolumes(next));
    }

    private void Apply(Action change)
    {
        try
        {
            change();
        }
        catch (COMException e)
        {
            _log.Warn("audio", $"Cannot change the volume of {_node.Description}: {e.Message}");
        }
    }

    private void OnPropertiesChanged()
    {
        Changed(nameof(Properties));
        Set(ref _description, _node.Description, nameof(Description));
    }

    private void OnAudioChanged()
    {
        var old = _volumes;
        _volumes = _node.Volumes;
        Changed(!old.SequenceEqual(_volumes), nameof(Volumes));
        Changed(Average(old) != Average(_volumes), nameof(Volume));
        Set(ref _muted, _node.Muted, nameof(Muted));
    }

    private void OnActiveChanged()
    {
        Set(ref _active, _node.Active, nameof(Active));
    }
}

public sealed class AudioService : ObservableObject, IDisposable
{
    private readonly IAudioBackend _graph;
    private readonly Log _log;
    private AudioNode? _defaultPlayback;
    private AudioNode? _defaultRecording;

    internal AudioService(IEventLoop loop, Log log)
        : this(new AudioGraph(loop, log), log)
    {
    }

    internal AudioService(IAudioBackend graph, Log log)
    {
        _log = log;
        _graph = graph;
        Refresh();
        OnDefaultsChanged();
        _graph.NodeAdded += OnNodeChanged;
        _graph.NodeRemoved += OnNodeChanged;
        _graph.DefaultsChanged += OnDefaultsChanged;
    }

    public ObservableList<AudioNode> Nodes { get => Read(field); } = [];

    public AudioNode? DefaultPlaybackDevice
    {
        get => Read(_defaultPlayback);
        set => SetDefault(value, true);
    }

    public AudioNode? DefaultRecordingDevice
    {
        get => Read(_defaultRecording);
        set => SetDefault(value, false);
    }

    public bool Ready => Read(_graph).Ready;

    internal AudioNode? Find(AudioGraphNode? node)
    {
        return Nodes.FirstOrDefault(n => n.Node == node);
    }

    private void SetDefault(AudioNode? node, bool playback)
    {
        if (node is null)
            return;
        var kind = playback ? "playback" : "recording";
        if (node.IsSession || node.IsPlayback != playback)
        {
            _log.Warn("audio", $"Cannot make {node.Description} the default {kind} device: it is not a {kind} device");
            return;
        }

        _graph.SetDefault(node.Name);
    }

    private void OnNodeChanged(AudioGraphNode _)
    {
        Refresh();
    }

    private void Refresh()
    {
        Nodes.Sync(_graph.Nodes, n => n.Node, n => n, n => new AudioNode(this, n, _log), static (_, _) => { },
            n => n.Remove());
    }

    private void OnDefaultsChanged()
    {
        Set(ref _defaultPlayback, Find(_graph.DefaultPlayback), nameof(DefaultPlaybackDevice));
        Set(ref _defaultRecording, Find(_graph.DefaultRecording), nameof(DefaultRecordingDevice));
    }

    public void Dispose()
    {
        _graph.NodeAdded -= OnNodeChanged;
        _graph.NodeRemoved -= OnNodeChanged;
        _graph.DefaultsChanged -= OnDefaultsChanged;
        foreach (var node in Nodes)
            node.Remove();
        _graph.Dispose();
    }
}
