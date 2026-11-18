namespace Winshell.Audio;

public abstract class AudioDevice : AudioVolume
{
    private protected AudioDevice(AudioGraphNode node, Log log)
        : base(node, log)
    {
        FormFactor = node.FormFactor;
    }

    public AudioFormFactor FormFactor { get => Read(field); }

    public ObservableList<AudioSession> Sessions { get => Read(field); } = [];

    private protected override void OnRemoved()
    {
        foreach (var session in Sessions)
            session.Remove();
    }
}

public sealed class PlaybackDevice : AudioDevice
{
    internal PlaybackDevice(AudioGraphNode node, Log log)
        : base(node, log)
    {
    }
}

public sealed class RecordingDevice : AudioDevice
{
    internal RecordingDevice(AudioGraphNode node, Log log)
        : base(node, log)
    {
    }
}
