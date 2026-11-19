using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32.Media.Audio;
using Winshell.Apps;
using Winshell.Native;

namespace Winshell.Audio;

internal sealed class SessionNode : AudioGraphNode
{
    private readonly AudioBackend _graph;
    private readonly IAudioSessionControl2 _control;
    private readonly ISimpleAudioVolume _volume;
    private readonly SessionEvents _events;
    private readonly uint _processId;
    private Process? _process;
    private EventHandler? _exited;
    private bool _released;

    public SessionNode(AudioBackend graph, EndpointNode endpoint, IAudioSessionControl2 control, string instanceId,
        AudioSessionState state)
        : base(graph.NextId())
    {
        _graph = graph;
        _control = control;
        Endpoint = endpoint;
        InstanceId = instanceId;
        ApplyActive(state == AudioSessionState.AudioSessionStateActive);
        Type = endpoint.Type == AudioNodeType.PlaybackDevice ? AudioNodeType.PlaybackSession : AudioNodeType.RecordingSession;
        Channels = endpoint.Channels;
        control.GetProcessId(out _processId);
        IsSystem = control.IsSystemSoundsSession() == 0;
        _volume = (ISimpleAudioVolume)control;
        ReadProperties();
        ReadVolume();
        _events = new SessionEvents(this);
        control.RegisterAudioSessionNotification(_events);
    }

    public EndpointNode Endpoint { get; }

    public override AudioGraphNode Device => Endpoint;

    public string InstanceId { get; }

    public bool IsSystem { get; }

    public override IAudioMeter? Meter =>
        _released || _control is not IAudioMeterInformation meter ? null : new WindowsAudioMeter(meter);

    public override void SetVolumes(IReadOnlyList<float> volumes)
    {
        if (volumes.Count == 0 || _released)
            return;
        var context = Guid.Empty;
        _volume.SetMasterVolume(Math.Clamp(volumes.Average(), 0, 1), ref context);
        ReadVolume();
    }

    public override void SetMuted(bool muted)
    {
        if (_released)
            return;
        var context = Guid.Empty;
        _volume.SetMute(muted, ref context);
        ReadVolume();
    }

    public void Watch()
    {
        if (_processId == 0)
            return;
        try
        {
            _process = Process.GetProcessById((int)_processId);
            _exited = (_, _) => _graph.Post(() => _graph.RemoveSession(this));
            _process.EnableRaisingEvents = true;
            _process.Exited += _exited;
            if (_process.HasExited)
                _graph.Post(() => _graph.RemoveSession(this));
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception)
        {
            _process = null;
        }
    }

    public void Release()
    {
        if (_released)
            return;
        _released = true;
        if (_process is not null)
        {
            if (_exited is not null)
                StopWatching(_process, _exited);
            _process.Dispose();
            _process = null;
            _exited = null;
        }

        try
        {
            _control.UnregisterAudioSessionNotification(_events);
        }
        catch (COMException)
        {
        }

        Winshell.Native.Com.Release(_control);
    }

    internal static void StopWatching(Process process, EventHandler handler)
    {
        process.EnableRaisingEvents = false;
        process.Exited -= handler;
    }

    private void ReadVolume()
    {
        _volume.GetMasterVolume(out var level);
        _volume.GetMute(out var muted);
        ApplyAudio(Enumerable.Repeat(level, Math.Max(1, Channels.Length)).ToArray(), muted);
    }

    private void ReadProperties()
    {
        var system = IsSystem;
        _control.GetDisplayName(out var displayName);
        displayName = IndirectString.Load(displayName ?? "");
        var path = _processId == 0 ? null : ProcessImage.TryRead(_processId);
        var programName = path is null ? "" : Path.GetFileNameWithoutExtension(path);
        var appName = system ? "System Sounds"
            : displayName.Length > 0 ? displayName
            : path is not null && AppIdentity.ProgramDescription(path) is { } description ? description
            : programName;
        Name = system ? "system-sounds" : programName.Length > 0 ? programName : $"session-{Id}";
        Description = appName;
    }

    private void OnState(AudioSessionState state)
    {
        if (_released)
            return;
        if (state == AudioSessionState.AudioSessionStateExpired)
        {
            _graph.RemoveSession(this);
            return;
        }

        ApplyActive(state == AudioSessionState.AudioSessionStateActive);
    }

    private sealed class SessionEvents(SessionNode node) : IAudioSessionEvents
    {
        public int OnDisplayNameChanged(string name, ref Guid context)
        {
            node._graph.Post(() =>
            {
                if (node._released)
                    return;
                node.ReadProperties();
                node.ApplyDescription();
            });
            return 0;
        }

        public int OnIconPathChanged(string path, ref Guid context)
        {
            return 0;
        }

        public int OnSimpleVolumeChanged(float volume, bool mute, ref Guid context)
        {
            node._graph.Post(() =>
            {
                if (!node._released)
                    node.ReadVolume();
            });
            return 0;
        }

        public int OnChannelVolumeChanged(uint count, nint volumes, uint changedChannel, ref Guid context)
        {
            return 0;
        }

        public int OnGroupingParamChanged(ref Guid param, ref Guid context)
        {
            return 0;
        }

        public int OnStateChanged(AudioSessionState state)
        {
            node._graph.Post(() => node.OnState(state));
            return 0;
        }

        public int OnSessionDisconnected(int reason)
        {
            node._graph.Post(() => node._graph.RemoveSession(node));
            return 0;
        }
    }
}
