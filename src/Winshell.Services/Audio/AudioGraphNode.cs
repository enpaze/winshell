namespace Winshell.Audio;

internal abstract class AudioGraphNode(int id)
{
    private float[] _volumes = [];

    public int Id { get; } = id;

    public string Name { get; protected set; } = "";

    public string Description { get; protected set; } = "";

    public string InterfaceName { get; protected init; } = "";

    protected AudioNodeType Type { get; init; }

    public bool IsPlayback => Type is AudioNodeType.PlaybackDevice or AudioNodeType.PlaybackSession;

    public bool IsSession => Type is AudioNodeType.PlaybackSession or AudioNodeType.RecordingSession;

    public virtual AudioGraphNode? Device => null;

    public bool Active { get; private set; }

    public Dictionary<string, string> Properties { get; protected set; } = [];

    public AudioChannel[] Channels { get; protected init; } = [];

    public IReadOnlyList<float> Volumes => _volumes;

    public bool Muted { get; private set; }

    public event Action? AudioChanged;

    public event Action? PropertiesChanged;

    public event Action? ActiveChanged;

    public abstract void SetVolumes(IReadOnlyList<float> volumes);

    public abstract void SetMuted(bool muted);

    public abstract IAudioMeter? Meter { get; }

    public virtual IDisposable? KeepRunning()
    {
        return null;
    }

    protected void ApplyAudio(float[] volumes, bool muted)
    {
        if (volumes.AsSpan().SequenceEqual(_volumes) && muted == Muted)
            return;
        _volumes = volumes;
        Muted = muted;
        AudioChanged?.Invoke();
    }

    protected void ApplyProperties()
    {
        PropertiesChanged?.Invoke();
    }

    protected void ApplyActive(bool active)
    {
        if (active == Active)
            return;
        Active = active;
        ActiveChanged?.Invoke();
    }
}
