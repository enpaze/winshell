using Winshell.Network;

namespace Winshell.Services.Tests;

public sealed class NetworkConnectionsTests
{
    private static readonly Guid AdapterId = Guid.Parse("0b0f2811-87b5-48d6-9d7a-7a26ae12ff33");

    // Windows lists a saved Wi-Fi profile on the adapter that saved it, before the connected one, with no
    // connectivity, no signal and its name standing in for the SSID.
    private static readonly NetworkConnectionInfo Saved = new(
        new NetworkProfileInfo("saved", "Old network", AdapterId, NetworkConnectivity.None, NetworkCost.Unrestricted,
            false, false, 0), "Old network", WifiSecurityType.Unknown);

    private static NetworkConnectionInfo Connected(NetworkConnectivity connectivity)
    {
        return new NetworkConnectionInfo(
            new NetworkProfileInfo("connected", "Home", AdapterId, connectivity, NetworkCost.Unrestricted, false,
                false, .8), "Home SSID", WifiSecurityType.Wpa2Psk);
    }

    private static NetworkInfo Scanned(string ssid, double strength)
    {
        return new NetworkInfo(ssid, ConnectionState.Disconnected, false, strength, WifiSecurityType.Wpa2Psk);
    }

    [Theory]
    [InlineData(NetworkConnectivity.InternetAccess)]
    [InlineData(NetworkConnectivity.LocalAccess)]
    [InlineData(NetworkConnectivity.CaptivePortal)]
    public void Profiles_leave_out_a_saved_profile_that_is_not_connected(NetworkConnectivity connectivity)
    {
        var connections = new NetworkConnections([Saved, Connected(connectivity)]);

        Assert.Equal(["Home"], connections.Profiles.Select(p => p.Name));
    }

    [Theory]
    [InlineData(true, .6)]
    [InlineData(false, .8)]
    public void A_wifi_adapter_is_on_the_network_of_its_connected_profile(bool inScan,
        double expectedSignal)
    {
        var connections = new NetworkConnections([Saved, Connected(NetworkConnectivity.InternetAccess)]);
        NetworkInfo[] scan = inScan ? [Scanned("Old network", .9), Scanned("Home SSID", .6)] : [];

        var networks = connections.WifiNetworks(AdapterId, ConnectionState.Connected, scan, []);

        var network = Assert.Single(networks, n => n.State == ConnectionState.Connected);
        Assert.Equal(("Home SSID", expectedSignal), (network.Name, network.SignalStrength));
    }
}
