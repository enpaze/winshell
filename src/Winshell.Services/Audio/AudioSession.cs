namespace Winshell.Audio;

public sealed class AudioSession : AudioVolume
{
    private bool _active;

    internal AudioSession(AudioDevice device, AudioGraphNode node, Log log)
        : base(node, log)
    {
        Device = device;
        _active = node.Active;
        node.ActiveChanged += OnActiveChanged;
    }

    public AudioDevice Device { get => Read(field); }

    public bool Active => Read(_active);

    private protected override void OnRemoved()
    {
        Node.ActiveChanged -= OnActiveChanged;
    }

    private void OnActiveChanged()
    {
        Set(ref _active, Node.Active, nameof(Active));
    }
}
