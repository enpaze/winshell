using System.Net.NetworkInformation;
using System.Security;
using Microsoft.Win32;
using Windows.Devices.Radios;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;
using Winshell.Native;
using WinNetworkInformation = Windows.Networking.Connectivity.NetworkInformation;

namespace Winshell.Network;

internal sealed record NetworkInfo(
    string Name,
    ConnectionState State,
    bool Known,
    double SignalStrength,
    WifiSecurityType Security,
    IReadOnlyList<WifiProfileInfo>? SavedProfiles = null);

internal sealed record WifiProfileInfo(
    string Id,
    string Name,
    string Ssid,
    bool AutoConnect,
    WifiSecurityType Security,
    bool HasPassword,
    bool Loaded,
    bool Hidden,
    string Error);

internal sealed record NetworkDeviceInfo(
    Guid Id,
    DeviceType Type,
    string Name,
    string Address,
    ConnectionState State,
    bool HasLink,
    uint LinkSpeed,
    IReadOnlyList<NetworkInfo> Networks,
    WifiDeviceMode Mode = WifiDeviceMode.Unknown);

internal sealed record NetworkProfileInfo(
    string Id,
    string Name,
    Guid AdapterId,
    NetworkConnectivity Connectivity,
    NetworkCost Cost,
    bool Roaming,
    bool OverDataLimit,
    double? SignalStrength);

internal sealed record NetworkSnapshot(
    bool Ready,
    IReadOnlyList<NetworkDeviceInfo> Devices,
    NetworkConnectivity Connectivity,
    RadioReading Wifi,
    IReadOnlyList<NetworkProfileInfo>? Profiles = null,
    NetworkAccessStatus Access = NetworkAccessStatus.Unspecified,
    AirplaneModeState AirplaneMode = AirplaneModeState.Unknown,
    string Error = "")
{
    public static readonly NetworkSnapshot Initial = new(false, [], NetworkConnectivity.Unknown, RadioReading.None);

    public bool WifiEnabled => Wifi.State == RadioState.On;
    public bool WifiHardwareEnabled => Wifi.State is RadioState.On or RadioState.Off;

    public WifiRadioState WifiState =>
        Wifi.Map(WifiRadioState.On, WifiRadioState.Off, WifiRadioState.Disabled, WifiRadioState.Unknown);
}

internal sealed class NetworkBackend(Log log) : INetworkBackend
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, WiFiAdapter> _wifiAdapters = [];
    private NetworkAccessStatus _access;
    private readonly SemaphoreSlim _accessGate = new(1, 1);
    private readonly List<Radio> _radios = [];
    private string _radioError = "";
    private IReadOnlySet<Guid>? _wifiIds;
    private readonly ProfileWriteQueue _profileWrites = new();
    private bool _watching;

    public event Action? Changed;

    private void Watch()
    {
        _watching = true;
        WinNetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        Wlan.Listen(Refresh, e => log.Warn("network", e.Message));
    }

    public void Dispose()
    {
        if (_watching)
        {
            _watching = false;
            WinNetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            Wlan.StopListening();
        }

        lock (_lock)
        {
            foreach (var radio in _radios)
                radio.StateChanged -= OnRadioStateChanged;
            _radios.Clear();
        }
    }

    private void Refresh()
    {
        Changed?.Invoke();
    }

    private void OnNetworkStatusChanged(object? sender)
    {
        Refresh();
    }

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    internal static bool WifiDevicesChanged(IReadOnlySet<Guid>? known, IReadOnlySet<Guid> now)
    {
        return known is null || !known.SetEquals(now);
    }

    private async Task ReloadWifiAsync()
    {
        List<Radio> radios = [];
        string? radioError = null;
        try
        {
            radios.AddRange((await Radio.GetRadiosAsync()).Where(r => r.Kind == RadioKind.WiFi));
        }
        catch (UnauthorizedAccessException e)
        {
            radioError = e.Message;
        }

        bool allowed;
        lock (_lock)
        {
            allowed = _access == NetworkAccessStatus.Allowed;
        }

        var adapters = allowed ? await WiFiAdapter.FindAllAdaptersAsync() : null;

        lock (_lock)
        {
            _radioError = radioError ?? "";
            foreach (var radio in _radios)
                radio.StateChanged -= OnRadioStateChanged;
            _radios.Clear();
            foreach (var radio in radios)
            {
                radio.StateChanged += OnRadioStateChanged;
                _radios.Add(radio);
            }

            if (adapters is not null && _access == NetworkAccessStatus.Allowed)
                SetAdapters(adapters);
        }
    }

    private void SetAdapters(IReadOnlyList<WiFiAdapter>? adapters)
    {
        _wifiAdapters.Clear();
        if (adapters is not null)
            foreach (var adapter in adapters)
                _wifiAdapters[adapter.NetworkAdapter.NetworkAdapterId] = adapter;
    }

    private void OnRadioStateChanged(Radio sender, object args)
    {
        Refresh();
    }

    public async Task RequestAccessAsync()
    {
        await _accessGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var access = await WiFiAdapter.RequestAccessAsync();
            var adapters = access == WiFiAccessStatus.Allowed ? await WiFiAdapter.FindAllAdaptersAsync() : null;
            lock (_lock)
            {
                SetAdapters(adapters);
                _access = MapAccess(access);
            }
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            lock (_lock)
            {
                _wifiAdapters.Clear();
                _access = NetworkAccessStatus.Unspecified;
            }

            throw;
        }
        finally
        {
            _accessGate.Release();
        }
    }

    internal static WifiDeviceMode MapDeviceMode(uint? operationMode)
    {
        return operationMode switch
        {
            0x00000004 => WifiDeviceMode.Station,
            0x00000008 => WifiDeviceMode.AccessPoint,
            _ => WifiDeviceMode.Unknown
        };
    }

    private static NetworkAccessStatus MapAccess(WiFiAccessStatus status)
    {
        return status switch
        {
            WiFiAccessStatus.Allowed => NetworkAccessStatus.Allowed,
            WiFiAccessStatus.DeniedByUser => NetworkAccessStatus.DeniedByUser,
            WiFiAccessStatus.DeniedBySystem => NetworkAccessStatus.DeniedBySystem,
            _ => NetworkAccessStatus.Unspecified
        };
    }

    public Task<NetworkSnapshot> ReadAsync(CancellationToken cancellation)
    {
        return Task.Run(ReadNowAsync, cancellation);
    }

    private async Task<NetworkSnapshot> ReadNowAsync()
    {
        if (!_watching)
            Watch();
        try
        {
            var snapshot = ReadState();
            var ids = snapshot.Devices.Where(d => d.Type == DeviceType.Wifi).Select(d => d.Id).ToHashSet();
            if (!WifiDevicesChanged(_wifiIds, ids))
                return snapshot;
            await ReloadWifiAsync().ConfigureAwait(false);
            _wifiIds = ids;
            return ReadState();
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            return NetworkSnapshot.Initial with { Error = e.Message };
        }
    }

    private NetworkSnapshot ReadState()
    {
        var connections = new NetworkConnections(WinNetworkInformation.GetConnectionProfiles()
            .Where(p => p.NetworkAdapter is not null).Select(ReadConnection));
        var wlanStates = Wlan.InterfaceStates();

        var devices = new List<NetworkDeviceInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            var type = nic.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Wireless80211 => DeviceType.Wifi,
                NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
                    or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx => DeviceType.Wired,
                _ => DeviceType.None
            };
            if (type == DeviceType.None || !Guid.TryParse(nic.Id, out var id))
                continue;
            if (IpHelper.GetInterface(id) is not { HardwareInterface: true } row)
                continue;
            if (type == DeviceType.Wired && nic.Description.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
                continue;

            var up = nic.OperationalStatus == OperationalStatus.Up;
            var profile = connections.ForAdapter(id)?.Profile;
            var address = MacAddress.Format(nic.GetPhysicalAddress().GetAddressBytes());
            var hasLink = row.MediaConnected;
            var speed = (uint)Math.Min(uint.MaxValue, row.LinkSpeed / 1_000_000);

            if (type == DeviceType.Wired)
            {
                var state = up ? ConnectionState.Connected : ConnectionState.Disconnected;
                NetworkInfo[] networks = hasLink
                    ? [new NetworkInfo(profile?.Name ?? nic.Name, state, true, 0, WifiSecurityType.Unknown)]
                    : [];
                devices.Add(new NetworkDeviceInfo(id, type, nic.Name, address, state, hasLink, hasLink ? speed : 0,
                    networks));
            }
            else
            {
                var state = wlanStates.TryGetValue(id, out var wlan)
                    ? wlan switch
                    {
                        WlanInterfaceState.Connected or WlanInterfaceState.AdHocNetworkFormed => ConnectionState
                            .Connected,
                        WlanInterfaceState.Associating or WlanInterfaceState.Discovering
                            or WlanInterfaceState.Authenticating => ConnectionState.Connecting,
                        WlanInterfaceState.Disconnecting => ConnectionState.Disconnecting,
                        WlanInterfaceState.Disconnected => ConnectionState.Disconnected,
                        _ => ConnectionState.Unknown
                    }
                    : up
                        ? ConnectionState.Connected
                        : ConnectionState.Disconnected;
                devices.Add(new NetworkDeviceInfo(id, type, nic.Name, address, state, false, 0,
                    ReadWifiNetworks(id, state, connections), MapDeviceMode(Wlan.OperationMode(id))));
            }
        }

        var connectivity =
            MapConnectivity(WinNetworkInformation.GetInternetConnectionProfile()?.GetNetworkConnectivityLevel() ??
                            NetworkConnectivityLevel.None);

        RadioReading wifi;
        string radioError;
        lock (_lock)
        {
            wifi = RadioReading.Read([.. _radios.Select(static radio => new WindowsRadio(radio))]);
            radioError = _radioError;
        }

        return new NetworkSnapshot(true, devices, connectivity, wifi, connections.Profiles, _access,
            ReadAirplaneMode(), radioError);
    }

    private static NetworkConnectionInfo ReadConnection(ConnectionProfile profile)
    {
        var cost = profile.GetConnectionCost();
        var info = new NetworkProfileInfo(
            WifiFields.Id(profile.NetworkAdapter.NetworkAdapterId, profile.ProfileName),
            profile.ProfileName, profile.NetworkAdapter.NetworkAdapterId,
            MapConnectivity(profile.GetNetworkConnectivityLevel()), MapCost(cost.NetworkCostType), cost.Roaming,
            cost.OverDataLimit, WifiFields.Bars(profile.GetSignalBars()));
        var ssid = ConnectedSsid(profile);
        return new NetworkConnectionInfo(info, ssid,
            ssid is null ? WifiSecurityType.Unknown : WifiFields.Security(profile.NetworkSecuritySettings));
    }

    private List<NetworkInfo> ReadWifiNetworks(Guid id, ConnectionState deviceState, NetworkConnections connections)
    {
        var saved = ReadSavedProfiles(id);
        var scanned = Adapter(id)?.NetworkReport.AvailableNetworks.Select(network => new NetworkInfo(network.Ssid,
            ConnectionState.Disconnected, false, WifiFields.Rssi(network.NetworkRssiInDecibelMilliwatts),
            WifiFields.Security(network.SecuritySettings))) ?? [];
        return connections.WifiNetworks(id, deviceState, scanned, saved);
    }

    private List<WifiProfileInfo> ReadSavedProfiles(Guid id)
    {
        var saved = new List<WifiProfileInfo>();
        foreach (var name in Wlan.ProfileNames(id).Order(StringComparer.Ordinal))
        {
            if (name.Length == 0)
                continue;
            string? xml;
            uint error;
            try
            {
                error = Wlan.GetProfile(id, name, out xml, out _);
            }
            catch (Exception e) when (WindowsFailure.IsExpected(e))
            {
                saved.Add(WifiProfiles.Describe(id, name, null, 0));
                continue;
            }

            saved.Add(WifiProfiles.Describe(id, name, xml, error));
        }

        return saved;
    }

    private static string? ConnectedSsid(ConnectionProfile profile)
    {
        if (!profile.IsWlanConnectionProfile)
            return null;
        var ssid = profile.WlanConnectionProfileDetails?.GetConnectedSsid();
        return string.IsNullOrEmpty(ssid) ? profile.ProfileName : ssid;
    }

    private WiFiAdapter? Adapter(Guid id)
    {
        lock (_lock)
        {
            return _wifiAdapters.GetValueOrDefault(id);
        }
    }

    public async Task<ConnectionFailReason?> ConnectAsync(Guid device, string ssid, string? psk)
    {
        var adapter = Adapter(device);
        var network = adapter?.NetworkReport.AvailableNetworks
            .Where(n => n.Ssid == ssid)
            .OrderByDescending(n => n.NetworkRssiInDecibelMilliwatts)
            .FirstOrDefault();
        if (adapter is null || network is null)
            return ConnectionFailReason.NetworkUnavailable;
        var result = psk is null
            ? await adapter.ConnectAsync(network, WiFiReconnectionKind.Automatic)
            : await adapter.ConnectAsync(network, WiFiReconnectionKind.Automatic,
                new PasswordCredential { Password = psk });
        Refresh();
        return MapConnectionResult(result.ConnectionStatus);
    }

    internal static ConnectionFailReason? MapConnectionResult(WiFiConnectionStatus status)
    {
        return status switch
        {
            WiFiConnectionStatus.Success => null,
            WiFiConnectionStatus.InvalidCredential => ConnectionFailReason.InvalidPassword,
            WiFiConnectionStatus.Timeout => ConnectionFailReason.Timeout,
            WiFiConnectionStatus.NetworkNotAvailable => ConnectionFailReason.NetworkUnavailable,
            WiFiConnectionStatus.UnsupportedAuthenticationProtocol => ConnectionFailReason.UnsupportedAuthentication,
            _ => ConnectionFailReason.Unknown
        };
    }

    public Task<string?> DisconnectAsync(Guid device)
    {
        return Task.Run<string?>(() =>
        {
            if (Adapter(device) is not { } adapter)
                return "Windows cannot disconnect this adapter through WiFiAdapter.";
            adapter.Disconnect();
            Refresh();
            return null;
        });
    }

    public Task<string?> ForgetAsync(Guid device, string profileName)
    {
        return CallWlan(() => Wlan.DeleteProfile(device, profileName));
    }

    public Task<string?> SetProfileAutoConnectAsync(Guid device, string profileName, bool autoConnect)
    {
        return ChangeProfile(() => WifiProfileEdit.SetAutoConnect(device, profileName, autoConnect));
    }

    public Task<string?> ClearProfilePasswordAsync(Guid device, string profileName)
    {
        return ChangeProfile(() => WifiProfileEdit.ClearPassword(device, profileName));
    }

    public Task<string?> ConnectProfileAsync(Guid device, string profileName, bool hidden)
    {
        return CallWlan(() => Wlan.ConnectProfile(device, profileName, hidden));
    }

    private Task<string?> CallWlan(Func<uint> call)
    {
        return Task.Run(() =>
        {
            var error = call();
            Refresh();
            return error == 0 ? null : WifiProfileEdit.WindowsMessage(error);
        });
    }

    private Task<string?> ChangeProfile(Func<ProfileChange> change)
    {
        return _profileWrites.RunAsync(() =>
        {
            try
            {
                var result = change();
                if (result.Wrote)
                    Refresh();
                return result.Error;
            }
            catch (Exception e) when (WindowsFailure.IsExpected(e))
            {
                return e.Message;
            }
        });
    }

    public async Task<string?> ScanAsync(Guid device)
    {
        const string needsAccess = "Call NetworkService.RequestAccess() before scanning Wi-Fi networks.";
        try
        {
            var adapter = Adapter(device);
            if (adapter is null)
                return needsAccess;
            try
            {
                await adapter.ScanAsync();
                return null;
            }
            catch (UnauthorizedAccessException e)
            {
                lock (_lock)
                {
                    _wifiAdapters.Clear();
                    _access = NetworkAccessStatus.DeniedBySystem;
                }

                return e.Message;
            }
        }
        finally
        {
            Refresh();
        }
    }

    internal static NetworkConnectivity MapConnectivity(NetworkConnectivityLevel value)
    {
        return value switch
        {
            NetworkConnectivityLevel.InternetAccess => NetworkConnectivity.InternetAccess,
            NetworkConnectivityLevel.ConstrainedInternetAccess => NetworkConnectivity.CaptivePortal,
            NetworkConnectivityLevel.LocalAccess => NetworkConnectivity.LocalAccess,
            _ => NetworkConnectivity.None
        };
    }

    private static NetworkCost MapCost(NetworkCostType value)
    {
        return value switch
        {
            NetworkCostType.Unrestricted => NetworkCost.Unrestricted,
            NetworkCostType.Fixed => NetworkCost.Fixed,
            NetworkCostType.Variable => NetworkCost.Variable,
            _ => NetworkCost.Unknown
        };
    }

    internal static AirplaneModeState MapAirplaneMode(object? value)
    {
        return value switch
        {
            0 => AirplaneModeState.Off,
            1 => AirplaneModeState.On,
            _ => AirplaneModeState.Unknown
        };
    }

    private static AirplaneModeState ReadAirplaneMode()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\RadioManagement");
            return MapAirplaneMode(key?.GetValue("SystemRadioState"));
        }
        catch (Exception e) when (e is UnauthorizedAccessException or SecurityException or IOException)
        {
            return AirplaneModeState.Unknown;
        }
    }
}
