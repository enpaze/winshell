using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32.Media.Audio;
using Winshell.Native;

namespace Winshell.Privacy;

/// <summary>
/// The recording streams that are open. Windows' consent store is not updated for every one of them, including the
/// stream the shell opens to meter a microphone. Armed only while <see cref="Start"/> is in effect. Call
/// <see cref="Start"/>, <see cref="Stop"/>, <see cref="Read"/> and <see cref="Dispose"/> on one thread; callbacks only
/// ask for another read.
/// </summary>
internal sealed class MicrophoneWatch : IDisposable
{
    private readonly Log _log;
    private readonly Action _changed;
    private readonly SessionEvents _sessionEvents;
    private readonly List<DeviceWatch> _devices = [];
    private IMMDeviceEnumerator? _devicesEnumerator;
    private DeviceEvents? _deviceEvents;
    private int _stale;
    private int _disposed;
    private int _logged;

    internal MicrophoneWatch(Log log, Action changed)
    {
        _log = log;
        _changed = changed;
        _sessionEvents = new SessionEvents(this);
    }

    internal void Start()
    {
        if (Volatile.Read(ref _disposed) != 0 || _devicesEnumerator is not null)
            return;
        try
        {
            _devicesEnumerator = CoreAudio.CreateEnumerator();
            _deviceEvents = new DeviceEvents(this);
            _devicesEnumerator.RegisterEndpointNotificationCallback(_deviceEvents);
            Interlocked.Exchange(ref _stale, 1);
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            LogOnce(e);
        }
    }

    internal void Stop()
    {
        ReleaseDevices();
        if (_devicesEnumerator is not null && _deviceEvents is not null)
            try
            {
                _devicesEnumerator.UnregisterEndpointNotificationCallback(_deviceEvents);
            }
            catch (COMException)
            {
            }

        CoreAudio.Release(_devicesEnumerator);
        _devicesEnumerator = null;
        _deviceEvents = null;
    }

    internal IReadOnlyList<MicrophoneCapture> Read()
    {
        if (Volatile.Read(ref _disposed) != 0 || _devicesEnumerator is null)
            return [];
        if (Interlocked.Exchange(ref _stale, 0) == 1)
            Bind();
        return Collect();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Stop();
    }

    private void Bind()
    {
        ReleaseDevices();
        if (_devicesEnumerator is null)
            return;
        IMMDeviceCollection? devices = null;
        try
        {
            _devicesEnumerator.EnumAudioEndpoints(EDataFlow.eCapture, DEVICE_STATE.DEVICE_STATE_ACTIVE, out devices);
            devices.GetCount(out var count);
            for (uint i = 0; i < count; i++)
            {
                devices.Item(i, out var device);
                Open(device);
            }
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            LogOnce(e);
        }
        finally
        {
            CoreAudio.Release(devices);
        }
    }

    /// <summary>Takes ownership of <paramref name="device"/> whatever happens.</summary>
    private void Open(IMMDevice device)
    {
        IAudioSessionManager2? manager = null;
        SessionCreated? created = null;
        DeviceWatch? watch = null;
        var kept = false;
        try
        {
            manager = CoreAudio.Activate<IAudioSessionManager2>(device);
            created = new SessionCreated(this);
            manager.RegisterSessionNotification(created);
            manager.GetSessionEnumerator(out var sessions);
            watch = new DeviceWatch(device, manager, created);
            try
            {
                sessions.GetCount(out var count);
                for (var i = 0; i < count; i++)
                {
                    sessions.GetSession(i, out var session);
                    if (!watch.Hold(session, _sessionEvents))
                        CoreAudio.Release(session);
                }
            }
            finally
            {
                CoreAudio.Release(sessions);
            }

            _devices.Add(watch);
            kept = true;
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            if (kept)
                return;
            if (watch is not null)
            {
                watch.Release(_sessionEvents);
            }
            else
            {
                if (manager is not null && created is not null)
                    try
                    {
                        manager.UnregisterSessionNotification(created);
                    }
                    catch (COMException)
                    {
                    }

                CoreAudio.Release(manager);
                CoreAudio.Release(device);
            }

            LogOnce(e);
        }
    }

    private List<MicrophoneCapture> Collect()
    {
        var found = new List<MicrophoneCapture>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in _devices)
        {
            IAudioSessionEnumerator? sessions = null;
            try
            {
                device.Manager.GetSessionEnumerator(out sessions);
                sessions.GetCount(out var count);
                for (var i = 0; i < count; i++)
                {
                    sessions.GetSession(i, out var session);
                    try
                    {
                        if (Capture(session) is { } capture && seen.Add(capture.PackageFamilyName.Length > 0
                                ? "pkg:" + capture.PackageFamilyName
                                : "exe:" + capture.Path))
                            found.Add(capture);
                    }
                    finally
                    {
                        CoreAudio.Release(session);
                    }
                }
            }
            catch (Exception e) when (e is COMException or InvalidCastException)
            {
                LogOnce(e);
            }
            finally
            {
                CoreAudio.Release(sessions);
            }
        }

        return found;
    }

    private static MicrophoneCapture? Capture(IAudioSessionControl2 session)
    {
        session.GetState(out var state);
        if (state != AudioSessionState.AudioSessionStateActive || session.IsSystemSoundsSession() == 0)
            return null;
        session.GetProcessId(out var processId);
        if (processId == 0)
            return null;
        var path = ProcessImage.TryRead(processId) ?? "";
        var family = PackageIdentity.FamilyName(processId);
        var name = Description(path);
        var mine = processId == (uint)Environment.ProcessId;
        return new MicrophoneCapture(name, path, family, mine);
    }

    private static string Description(string path)
    {
        if (path.Length == 0)
            return "";
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim() is { Length: > 0 } description
                ? description
                : "";
        }
        catch (Exception e) when (e is FileNotFoundException or IOException or UnauthorizedAccessException
                                      or ArgumentException)
        {
            return "";
        }
    }

    private void ReleaseDevices()
    {
        foreach (var device in _devices)
            device.Release(_sessionEvents);
        _devices.Clear();
    }

    private void Ask(bool rebind)
    {
        if (rebind)
            Interlocked.Exchange(ref _stale, 1);
        if (Volatile.Read(ref _disposed) != 0)
            return;
        _changed();
    }

    private void LogOnce(Exception exception)
    {
        if (Interlocked.Exchange(ref _logged, 1) == 0)
            _log.Warn("privacy", $"Recording streams could not be listed: {exception.Message}");
    }

    private sealed class DeviceWatch(IMMDevice device, IAudioSessionManager2 manager, SessionCreated created)
    {
        private readonly List<IAudioSessionControl2> _sessions = [];

        public IAudioSessionManager2 Manager { get; } = manager;

        public bool Hold(IAudioSessionControl2 session, SessionEvents events)
        {
            try
            {
                session.RegisterAudioSessionNotification(events);
            }
            catch (COMException)
            {
                return false;
            }

            _sessions.Add(session);
            return true;
        }

        public void Release(SessionEvents events)
        {
            try
            {
                Manager.UnregisterSessionNotification(created);
            }
            catch (COMException)
            {
            }

            foreach (var session in _sessions)
            {
                try
                {
                    session.UnregisterAudioSessionNotification(events);
                }
                catch (COMException)
                {
                }

                CoreAudio.Release(session);
            }

            _sessions.Clear();
            CoreAudio.Release(Manager);
            CoreAudio.Release(device);
        }
    }

    private sealed class DeviceEvents(MicrophoneWatch watch) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string deviceId, uint newState)
        {
            watch.Ask(true);
        }

        public void OnDeviceAdded(string deviceId)
        {
            watch.Ask(true);
        }

        public void OnDeviceRemoved(string deviceId)
        {
            watch.Ask(true);
        }

        public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId)
        {
        }

        public void OnPropertyValueChanged(string deviceId, PROPERTYKEY key)
        {
        }
    }

    private sealed class SessionCreated(MicrophoneWatch watch) : IAudioSessionNotification
    {
        public int OnSessionCreated(IAudioSessionControl2 session)
        {
            CoreAudio.Release(session);
            watch.Ask(true);
            return 0;
        }
    }

    private sealed class SessionEvents(MicrophoneWatch watch) : IAudioSessionEvents
    {
        public int OnDisplayNameChanged(string name, ref Guid context)
        {
            return 0;
        }

        public int OnIconPathChanged(string path, ref Guid context)
        {
            return 0;
        }

        public int OnSimpleVolumeChanged(float volume, bool mute, ref Guid context)
        {
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
            watch.Ask(false);
            return 0;
        }

        public int OnSessionDisconnected(int reason)
        {
            watch.Ask(true);
            return 0;
        }
    }
}
