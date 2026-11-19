using Winshell.Network;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class NetworkConnectionsTests
{
    private static readonly Guid AdapterId = Guid.Parse("0b0f2811-87b5-48d6-9d7a-7a26ae12ff33");

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

    [TestMethod]
    [DataRow(NetworkConnectivity.InternetAccess)]
    [DataRow(NetworkConnectivity.LocalAccess)]
    [DataRow(NetworkConnectivity.CaptivePortal)]
    public void Profiles_leave_out_a_saved_profile_that_is_not_connected(NetworkConnectivity connectivity)
    {
        var connections = new NetworkConnections([Saved, Connected(connectivity)]);

        Assert.AreSequenceEqual(["Home"], connections.Profiles.Select(p => p.Name));
    }

    [TestMethod]
    [DataRow(true, .6)]
    [DataRow(false, .8)]
    public void A_wifi_adapter_is_on_the_network_of_its_connected_profile(bool inScan,
        double expectedSignal)
    {
        var connections = new NetworkConnections([Saved, Connected(NetworkConnectivity.InternetAccess)]);
        NetworkInfo[] scan = inScan ? [Scanned("Old network", .9), Scanned("Home SSID", .6)] : [];

        var networks = connections.WifiNetworks(AdapterId, ConnectionState.Connected, scan, []);

        var network = Assert.ContainsSingle(n => n.State == ConnectionState.Connected, networks);
        Assert.AreEqual(("Home SSID", expectedSignal), (network.Name, network.SignalStrength));
    }
}
