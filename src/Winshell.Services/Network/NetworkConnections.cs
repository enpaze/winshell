namespace Winshell.Network;

internal sealed record NetworkConnectionInfo(NetworkProfileInfo Profile, string? Ssid, WifiSecurityType Security);

internal sealed class NetworkConnections(IEnumerable<NetworkConnectionInfo> profiles)
{
    private readonly NetworkConnectionInfo[] _connected =
        profiles.Where(p => p.Profile.Connectivity != NetworkConnectivity.None).ToArray();

    public NetworkProfileInfo[] Profiles => _connected.Select(p => p.Profile).ToArray();

    public NetworkConnectionInfo? ForAdapter(Guid id)
    {
        return _connected.FirstOrDefault(p => p.Profile.AdapterId == id);
    }

    public List<NetworkInfo> WifiNetworks(Guid id, ConnectionState deviceState, IEnumerable<NetworkInfo> scanned,
        IReadOnlyList<WifiProfileInfo> saved)
    {
        var connection = ForAdapter(id);
        var connectedSsid = connection?.Ssid;
        var networks = new Dictionary<string, NetworkInfo>(StringComparer.Ordinal);
        foreach (var network in scanned)
        {
            if (string.IsNullOrEmpty(network.Name))
                continue;
            if (networks.TryGetValue(network.Name, out var seen) && seen.SignalStrength >= network.SignalStrength)
                continue;
            var profiles = ProfilesFor(network.Name);
            networks[network.Name] = network with
            {
                State = network.Name == connectedSsid ? deviceState : ConnectionState.Disconnected,
                Known = profiles.Length > 0,
                SavedProfiles = profiles
            };
        }

        if (connectedSsid is not null && !networks.ContainsKey(connectedSsid))
        {
            var profiles = ProfilesFor(connectedSsid);
            networks[connectedSsid] = new NetworkInfo(connectedSsid, deviceState, profiles.Length > 0,
                connection!.Profile.SignalStrength ?? 0, connection.Security, profiles);
        }

        return
        [
            .. networks.Values.OrderByDescending(n => n.State == ConnectionState.Connected)
                .ThenByDescending(n => n.SignalStrength)
        ];

        WifiProfileInfo[] ProfilesFor(string ssid)
        {
            return saved.Where(p => WifiProfiles.Matches(p, ssid)).ToArray();
        }
    }
}
