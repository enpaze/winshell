namespace Winshell.Audio;

/// <summary>
/// The devices and sessions Windows has. <see cref="AudioService"/> owns its graph.
/// </summary>
internal interface IAudioBackend : IDisposable
{
    IReadOnlyList<AudioGraphNode> Nodes { get; }

    bool Ready { get; }

    AudioGraphNode? DefaultPlayback { get; }

    AudioGraphNode? DefaultRecording { get; }

    event Action<AudioGraphNode>? NodeAdded;

    event Action<AudioGraphNode>? NodeRemoved;

    event Action? DefaultsChanged;

    void SetDefault(string endpointId);
}
