namespace Winshell.Network;

/// <summary>A Windows connection profile, with the SSID and security it connects with when it is a Wi-Fi one.</summary>
internal sealed record NetworkConnectionInfo(NetworkProfileInfo Profile, string? Ssid, WifiSecurityType Security);

/// <summary>The connection profiles connected on an adapter now, and the Wi-Fi networks each adapter sees.</summary>
internal sealed class NetworkConnections(IEnumerable<NetworkConnectionInfo> profiles)
{
    // Windows also lists saved profiles that are not connected, each with its adapter.
    private readonly NetworkConnectionInfo[] _connected =
        profiles.Where(p => p.Profile.Connectivity != NetworkConnectivity.None).ToArray();

    public NetworkProfileInfo[] Profiles => _connected.Select(p => p.Profile).ToArray();

    public NetworkConnectionInfo? ForAdapter(Guid id)
    {
        return _connected.FirstOrDefault(p => p.Profile.AdapterId == id);
    }

    /// <summary>
    /// The scanned networks, strongest first after the connected one. The connected network is added from its
    /// profile when the scan misses it.
    /// </summary>
    public List<NetworkInfo> WifiNetworks(Guid id, ConnectionState deviceState, IEnumerable<NetworkInfo> scanned,
        IReadOnlyList<WifiProfileInfo> saved)
    {
        var connection = ForAdapter(id);
        var connectedSsid = connection?.Ssid;
        var networks = new Dictionary<string, NetworkInfo>(StringComparer.Ordinal);
        foreach (var network in scanned)
        {
            // A hidden network has no name to show or connect by.
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
