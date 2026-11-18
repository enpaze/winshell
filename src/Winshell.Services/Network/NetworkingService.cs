using Windows.Devices.Radios;

namespace Winshell.Network;

public sealed class NetworkingService : ObservableObject, IDisposable
{
    private readonly IEventLoop _loop;
    private readonly INetworkBackend _backend;
    private readonly StateReader<NetworkSnapshot> _reader;
    private readonly DeviceAction _access;
    private readonly DeviceAction _wifiSwitch;
    private NetworkSnapshot _snapshot = NetworkSnapshot.Initial;
    private string _error = "";
    private string _radioError = "";
    private bool _disposed;

    internal NetworkingService(IEventLoop loop, Log log, INetworkBackend? backend = null)
    {
        _loop = loop;
        _backend = backend ?? new NetworkBackend(log);
        _access = new DeviceAction(loop.Post);
        _wifiSwitch = new DeviceAction(loop.Post);
        _reader = new StateReader<NetworkSnapshot>(_backend.ReadAsync, Apply, loop,
            new FailureWarning(log, "network"));
        _backend.Changed += _reader.Request;
        _reader.Request();
    }

    public event Action<string>? OperationFailed;

    public ObservableList<NetworkDevice> Devices { get => Read(field); } = [];

    public ObservableList<NetworkProfile> Profiles { get => Read(field); } = [];

    public bool Ready => Read(_snapshot).Ready;

    public bool WifiEnabled
    {
        get => Read(_snapshot).WifiEnabled;
        set => _ = SetWifiEnabledAsync(value);
    }

    public WifiRadioState WifiState => Read(_snapshot).WifiState;

    public bool WifiHardwareEnabled => Read(_snapshot).WifiHardwareEnabled;

    public NetworkAccessStatus AccessStatus => Read(_snapshot).Access;

    public AirplaneModeState AirplaneMode => Read(_snapshot).AirplaneMode;

    public NetworkConnectivity Connectivity => Read(_snapshot).Connectivity;

    public string Error => Read(_error);

    public bool RequestingAccess => Read(_access).Running;

    public void Refresh()
    {
        _reader.Request();
    }

    public void RequestAccess()
    {
        _ = RequestAccessAsync();
    }

    internal Task RequestAccessAsync()
    {
        if (_disposed)
            return Task.CompletedTask;
        return _access.RunAsync(() => Changed(nameof(RequestingAccess)), async () =>
        {
            try
            {
                await _backend.RequestAccessAsync().ConfigureAwait(false);
            }
            finally
            {
                await _reader.ReadAsync().ConfigureAwait(false);
            }

            return null;
        }, error =>
        {
            if (_disposed)
                return;
            _radioError = "";
            Set(ref _error, error, nameof(Error));
            if (error.Length > 0)
                OperationFailed?.Invoke(error);
        }, () =>
        {
            if (!_disposed)
                Changed(nameof(RequestingAccess));
        });
    }

    internal Task SetWifiEnabledAsync(bool enabled)
    {
        if (_disposed || _snapshot.WifiEnabled == enabled)
            return Task.CompletedTask;
        var radios = _snapshot.Wifi.Radios;
        return _wifiSwitch.RunAsync(static () => { }, async () =>
        {
            const string missing = "Windows reports no Wi-Fi radio.";
            var error = RadioSwitch.Describe(
                await RadioSwitch.SetEnabledAsync(RadioKind.WiFi, radios, enabled).ConfigureAwait(false), missing);
            if (error is null)
                await _reader.ReadAsync().ConfigureAwait(false);
            return error;
        }, error =>
        {
            if (_disposed)
                return;
            _radioError = error;
            Apply(_snapshot);
            if (error.Length > 0)
                OperationFailed?.Invoke(error);
        });
    }

    private void Apply(NetworkSnapshot snapshot)
    {
        if (_disposed)
            return;
        if (!ReferenceEquals(snapshot, _snapshot) &&
            (snapshot.Error.Length > 0 || _snapshot.WifiEnabled != snapshot.WifiEnabled))
            _radioError = "";
        var old = _snapshot;
        _snapshot = snapshot;
        Set(ref _error, _radioError.Length > 0 ? _radioError : snapshot.Error, nameof(Error));
        Changed(old.Ready != snapshot.Ready, nameof(Ready));
        Changed(old.WifiEnabled != snapshot.WifiEnabled, nameof(WifiEnabled));
        Changed(old.WifiHardwareEnabled != snapshot.WifiHardwareEnabled, nameof(WifiHardwareEnabled));
        Changed(old.WifiState != snapshot.WifiState, nameof(WifiState));
        Changed(old.Connectivity != snapshot.Connectivity, nameof(Connectivity));
        Changed(old.Access != snapshot.Access, nameof(AccessStatus));
        Changed(old.AirplaneMode != snapshot.AirplaneMode, nameof(AirplaneMode));
        Devices.Sync(snapshot.Devices, d => d.NativeId, i => i.Id, CreateDevice, (d, i) => d.Update(i),
            d => d.Remove());
        Profiles.Sync(snapshot.Profiles ?? [], p => p.Id, i => i.Id, i => new NetworkProfile(i), (p, i) => p.Update(i));
    }

    private NetworkDevice CreateDevice(NetworkDeviceInfo info)
    {
        return info.Type == DeviceType.Wifi
            ? new WifiDevice(info, _loop, _backend)
            : new WiredDevice(info, _loop, _backend);
    }

    public void Dispose()
    {
        _disposed = true;
        _backend.Changed -= _reader.Request;
        _reader.Stop(_backend.Dispose);
        foreach (var device in Devices)
            device.Remove();
    }
}
