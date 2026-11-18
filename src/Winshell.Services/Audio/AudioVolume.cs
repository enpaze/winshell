using System.Runtime.InteropServices;

namespace Winshell.Audio;

public abstract class AudioVolume : ObservableObject
{
    private readonly Log _log;
    private string _name;
    private IReadOnlyList<float> _volumes;
    private bool _muted;

    private protected AudioVolume(AudioGraphNode node, Log log)
    {
        Node = node;
        _log = log;
        _name = node.Description;
        _volumes = node.Volumes;
        _muted = node.Muted;
        Channels = node.Channels;
        node.DescriptionChanged += OnDescriptionChanged;
        node.AudioChanged += OnAudioChanged;
    }

    internal AudioGraphNode Node { get; }

    internal bool IsRemoved { get; private set; }

    internal event Action? Removed;

    public string Name => Read(_name);

    public bool Muted
    {
        get => Read(_muted);
        set => Apply(() => Node.SetMuted(value));
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
        Node.DescriptionChanged -= OnDescriptionChanged;
        Node.AudioChanged -= OnAudioChanged;
        OnRemoved();
        Removed?.Invoke();
    }

    private protected virtual void OnRemoved()
    {
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
        Apply(() => Node.SetVolumes(next));
    }

    private void Apply(Action change)
    {
        try
        {
            change();
        }
        catch (COMException e)
        {
            _log.Warn("audio", $"Cannot change the volume of {Node.Description}: {e.Message}");
        }
    }

    private void OnDescriptionChanged()
    {
        Set(ref _name, Node.Description, nameof(Name));
    }

    private void OnAudioChanged()
    {
        var old = _volumes;
        _volumes = Node.Volumes;
        Changed(!old.SequenceEqual(_volumes), nameof(Volumes));
        Changed(Average(old) != Average(_volumes), nameof(Volume));
        Set(ref _muted, Node.Muted, nameof(Muted));
    }
}
