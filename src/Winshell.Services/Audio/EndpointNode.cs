using System.Runtime.InteropServices;
using Windows.Win32.Media.Audio;
using Winshell.Native;

namespace Winshell.Audio;

internal sealed class EndpointNode : AudioGraphNode
{
    private readonly AudioGraph _graph;
    private readonly IMMDevice _device;
    private readonly IAudioEndpointVolume _volume;
    private readonly VolumeNotifications _volumeNotifications;
    private IAudioSessionManager2? _sessionManager;
    private SessionNotifications? _sessionNotifications;
    private Native.IAudioMeterInformation? _meter;
    private IAudioClient? _keepAlive;
    private int _keepAliveCount;
    private int _refreshPending;

    public EndpointNode(AudioGraph graph, IMMDevice device, string deviceId, bool render)
        : base(graph.NextId())
    {
        _graph = graph;
        _device = device;
        Type = render ? AudioNodeType.PlaybackDevice : AudioNodeType.RecordingDevice;
        Name = deviceId;
        device.OpenPropertyStore(0, out var store);
        byte[]? format;
        try
        {
            Description = PropertyStore.ReadString(store, CoreAudio.DeviceFriendlyName) ?? deviceId;
            InterfaceName = PropertyStore.ReadString(store, CoreAudio.DeviceDescription) ?? "";
            var formFactor = PropertyStore.ReadUInt(store, CoreAudio.EndpointFormFactor);
            Properties = new Dictionary<string, string>
            {
                [AudioProperty.EndpointId] = Name,
                [AudioProperty.FriendlyName] = Description,
                [AudioProperty.InterfaceName] = InterfaceName,
                [AudioProperty.Flow] = render ? "playback" : "recording",
                [AudioProperty.Adapter] = PropertyStore.ReadString(store, CoreAudio.InterfaceFriendlyName) ?? Description,
                [AudioProperty.Icon] = IconName(formFactor, render),
                [AudioProperty.FormFactor] = FormFactorName(formFactor)
            };
            format = PropertyStore.ReadBlob(store, CoreAudio.EngineDeviceFormat);
        }
        finally
        {
            Winshell.Native.Com.Release(store);
        }

        _volume = CoreAudio.Activate<IAudioEndpointVolume>(device);
        try
        {
            _volume.GetChannelCount(out var channels);
            Channels = ChannelLayout.FromFormat(format, (int)channels);
            ReadVolume();
            _volumeNotifications = new VolumeNotifications(this);
            _volume.RegisterControlChangeNotify(_volumeNotifications);
        }
        catch
        {
            Winshell.Native.Com.Release(_volume);
            throw;
        }
    }

    public List<SessionNode> Sessions { get; } = [];

    public override IAudioMeterInformation? Meter
    {
        get
        {
            if (Released)
                return null;
            try
            {
                _meter ??= CoreAudio.Activate<Native.IAudioMeterInformation>(_device);
            }
            catch (COMException)
            {
                return null;
            }

            return new WindowsAudioMeter(_meter);
        }
    }

    public override void SetVolumes(IReadOnlyList<float> volumes)
    {
        if (volumes.Count == 0 || Released)
            return;
        var context = Guid.Empty;
        if (volumes.All(v => v == volumes[0]))
            _volume.SetMasterVolumeLevelScalar(Math.Clamp(volumes[0], 0, 1), ref context);
        else
            for (var i = 0; i < volumes.Count; i++)
                _volume.SetChannelVolumeLevelScalar((uint)i, Math.Clamp(volumes[i], 0, 1), ref context);
        ReadVolume();
    }

    public override void SetMuted(bool muted)
    {
        if (Released)
            return;
        var context = Guid.Empty;
        _volume.SetMute(muted, ref context);
        ReadVolume();
    }

    public override IDisposable? KeepRunning()
    {
        if (IsPlayback || Released)
            return null;
        if (_keepAliveCount++ == 0)
            try
            {
                _keepAlive = CoreAudio.StartSharedStream(_device);
            }
            catch (COMException)
            {
            }

        return new DisposeAction(() =>
        {
            if (--_keepAliveCount == 0)
                StopKeepAlive();
        });
    }

    private void StopKeepAlive()
    {
        if (_keepAlive is not { } client)
            return;
        _keepAlive = null;
        try
        {
            client.Stop();
        }
        catch (COMException)
        {
        }

        Winshell.Native.Com.Release(client);
    }

    private void ReadVolume()
    {
        var volumes = new float[Channels.Length];
        for (var i = 0; i < volumes.Length; i++)
            _volume.GetChannelVolumeLevelScalar((uint)i, out volumes[i]);
        _volume.GetMute(out var muted);
        ApplyAudio(volumes, muted);
    }

    private void RequestRefresh()
    {
        if (Interlocked.Exchange(ref _refreshPending, 1) != 0)
            return;
        _graph.Post(() =>
        {
            Interlocked.Exchange(ref _refreshPending, 0);
            if (!Released)
                ReadVolume();
        });
    }

    private bool Released { get; set; }

    public void StartSessions()
    {
        try
        {
            _sessionManager = CoreAudio.Activate<IAudioSessionManager2>(_device);
            _sessionManager.GetSessionEnumerator(out var sessions);
            try
            {
                _sessionNotifications = new SessionNotifications(this);
                _sessionManager.RegisterSessionNotification(_sessionNotifications);
                sessions.GetCount(out var count);
                for (var i = 0; i < count; i++)
                {
                    sessions.GetSession(i, out var control);
                    AddSession(control);
                }
            }
            finally
            {
                Winshell.Native.Com.Release(sessions);
            }
        }
        catch (COMException e)
        {
            _graph.Log.Warn("audio", $"Cannot list the audio sessions of \"{Description}\": {e.Message}");
        }
    }

    private void AddSession(IAudioSessionControl2 control)
    {
        SessionNode? session = null;
        try
        {
            session = Open(control);
        }
        finally
        {
            if (session is null)
                Winshell.Native.Com.Release(control);
        }

        if (session is null)
            return;
        Sessions.Add(session);
        _graph.AddSession(session);
        session.Watch();
    }

    private SessionNode? Open(IAudioSessionControl2 control)
    {
        if (Released)
            return null;
        control.GetSessionInstanceIdentifier(out var instance);
        if (Sessions.Any(s => s.InstanceId == instance))
            return null;
        control.GetState(out var state);
        control.GetProcessId(out var processId);
        if (state == AudioSessionState.AudioSessionStateExpired
            || (!IsPlayback && (control.IsSystemSoundsSession() == 0 || processId == Environment.ProcessId)))
            return null;
        try
        {
            return new SessionNode(_graph, this, control, instance, state);
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            return null;
        }
    }

    public void Release()
    {
        if (Released)
            return;
        Released = true;
        try
        {
            _volume.UnregisterControlChangeNotify(_volumeNotifications);
            if (_sessionNotifications is not null)
                _sessionManager?.UnregisterSessionNotification(_sessionNotifications);
        }
        catch (COMException)
        {
        }

        StopKeepAlive();
        Winshell.Native.Com.Release(_meter);
        Winshell.Native.Com.Release(_sessionManager);
        Winshell.Native.Com.Release(_volume);
        Winshell.Native.Com.Release(_device);
    }

    private static string IconName(uint? formFactor, bool render)
    {
        return formFactor switch
        {
            1 => "audio-speakers",
            3 => "audio-headphones",
            5 or 6 => "audio-headset",
            4 => "audio-input-microphone",
            _ => render ? "audio-card" : "audio-input-microphone"
        };
    }

    private static string FormFactorName(uint? formFactor)
    {
        return formFactor switch
        {
            0 => "network",
            1 => "speaker",
            2 => "line",
            3 => "headphone",
            4 => "microphone",
            5 => "headset",
            6 => "handset",
            8 => "spdif",
            9 => "hdmi",
            _ => "unknown"
        };
    }

    private sealed class VolumeNotifications(EndpointNode node) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(nint data)
        {
            node.RequestRefresh();
            return 0;
        }
    }

    private sealed class SessionNotifications(EndpointNode node) : IAudioSessionNotification
    {
        public int OnSessionCreated(IAudioSessionControl2 session)
        {
            nint stream;
            try
            {
                stream = ComApartment.Capture(session);
            }
            catch (Exception e) when (e is COMException or InvalidCastException or InvalidOperationException)
            {
                node._graph.Log.Warn("audio", e.Message);
                return 0;
            }
            finally
            {
                Winshell.Native.Com.Release(session);
            }

            node._graph.Post(() => node.AddSession((IAudioSessionControl2)ComApartment.Restore(stream)));
            return 0;
        }
    }
}
