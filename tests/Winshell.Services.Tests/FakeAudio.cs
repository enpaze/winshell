using System.Runtime.InteropServices;
using Winshell.Audio;

namespace Winshell.Services.Tests;

internal sealed class FakeAudioBackend : IAudioBackend
{
    private readonly List<AudioGraphNode> _nodes = [];

    public IReadOnlyList<AudioGraphNode> Nodes => _nodes;

    public bool Ready => true;

    public AudioGraphNode? DefaultPlayback { get; private set; }

    public AudioGraphNode? DefaultRecording { get; private set; }

    public List<string> DefaultsAsked { get; } = [];

    public event Action<AudioGraphNode>? NodeAdded;

    public event Action<AudioGraphNode>? NodeRemoved;

    public event Action? DefaultsChanged;

    public void SetDefault(string endpointId)
    {
        DefaultsAsked.Add(endpointId);
    }

    public void Dispose()
    {
    }

    public FakeGraphNode Add(FakeGraphNode node)
    {
        _nodes.Add(node);
        NodeAdded?.Invoke(node);
        return node;
    }

    public void Remove(FakeGraphNode node)
    {
        _nodes.Remove(node);
        NodeRemoved?.Invoke(node);
    }

    public void SetDefaults(AudioGraphNode? playback, AudioGraphNode? recording)
    {
        DefaultPlayback = playback;
        DefaultRecording = recording;
        DefaultsChanged?.Invoke();
    }
}

internal sealed class FakeGraphNode : AudioGraphNode
{
    private static int _nextId;

    public FakeGraphNode(AudioNodeType type, string description = "Node", AudioChannel[]? channels = null,
        FakeGraphNode? device = null, AudioFormFactor formFactor = AudioFormFactor.Unknown)
        : base(Interlocked.Increment(ref _nextId))
    {
        Type = type;
        FormFactor = formFactor;
        Device = device;
        Description = description;
        Name = description;
        Channels = channels ?? [AudioChannel.FrontLeft, AudioChannel.FrontRight];
        ApplyAudio(new float[Channels.Length], false);
    }

    public override FakeGraphNode? Device { get; }

    public FakeMeter? FakeMeter { get; init; }

    public bool FailWrites { get; set; }

    public int Running { get; private set; }

    public override IAudioMeter? Meter => FakeMeter;

    public void SetAudio(float[] volumes, bool muted)
    {
        ApplyAudio(volumes, muted);
    }

    public void SetActive(bool active)
    {
        ApplyActive(active);
    }

    public void Rename(string description)
    {
        Description = description;
        ApplyDescription();
    }

    public override void SetVolumes(IReadOnlyList<float> volumes)
    {
        ThrowIfFailing();
        ApplyAudio([.. volumes], Muted);
    }

    public override void SetMuted(bool muted)
    {
        ThrowIfFailing();
        ApplyAudio([.. Volumes], muted);
    }

    public override IDisposable KeepRunning()
    {
        Running++;
        return new DisposeAction(() => Running--);
    }

    private void ThrowIfFailing()
    {
        if (FailWrites)
            throw new COMException("the device is gone");
    }
}

internal sealed class FakeMeter(params float[] peaks) : IAudioMeter
{
    public float[] Peaks { get; set; } = peaks;

    public bool Gone { get; set; }

    public int Reads { get; private set; }

    public float[] ReadPeaks()
    {
        Reads++;
        if (Gone)
            throw new COMException("the device is gone");
        return [.. Peaks];
    }
}
