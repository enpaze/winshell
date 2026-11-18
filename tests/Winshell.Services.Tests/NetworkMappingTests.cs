using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Winshell.Network;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class NetworkMappingTests
{
    [TestMethod]
    [DataRow(NetworkConnectivityLevel.None, NetworkConnectivity.None)]
    [DataRow(NetworkConnectivityLevel.LocalAccess, NetworkConnectivity.LocalAccess)]
    [DataRow(NetworkConnectivityLevel.ConstrainedInternetAccess, NetworkConnectivity.CaptivePortal)]
    [DataRow(NetworkConnectivityLevel.InternetAccess, NetworkConnectivity.InternetAccess)]
    public void Connectivity_mapping(NetworkConnectivityLevel value, NetworkConnectivity expected)
    {
        Assert.AreEqual(expected, NetworkBackend.MapConnectivity(value));
    }

    [TestMethod]
    [DataRow(null, AirplaneModeState.Unknown)]
    [DataRow(0, AirplaneModeState.Off)]
    [DataRow(1, AirplaneModeState.On)]
    [DataRow(42, AirplaneModeState.Unknown)]
    public void Airplane_mode_is_not_inferred_from_radios(object? value, AirplaneModeState expected)
    {
        Assert.AreEqual(expected, NetworkBackend.MapAirplaneMode(value));
    }

    [TestMethod]
    [DataRow(0x00000004u, WifiDeviceMode.Station)]
    [DataRow(0x00000008u, WifiDeviceMode.AccessPoint)]
    [DataRow(0x00000000u, WifiDeviceMode.Unknown)]
    [DataRow(0x00000001u, WifiDeviceMode.Unknown)]
    [DataRow(0x00000002u, WifiDeviceMode.Unknown)]
    [DataRow(0x00000010u, WifiDeviceMode.Unknown)]
    [DataRow(0x00000020u, WifiDeviceMode.Unknown)]
    [DataRow(0x00000040u, WifiDeviceMode.Unknown)]
    [DataRow(0x40000000u, WifiDeviceMode.Unknown)]
    [DataRow(0x80000000u, WifiDeviceMode.Unknown)]
    public void Operation_mode_maps_extensible_station_and_access_point(uint operationMode, WifiDeviceMode expected)
    {
        Assert.AreEqual(expected, NetworkBackend.MapDeviceMode(operationMode));
    }

    [TestMethod]
    public void A_failed_operation_mode_query_is_unknown()
    {
        Assert.AreEqual(WifiDeviceMode.Unknown, NetworkBackend.MapDeviceMode(null));
    }

    [TestMethod]
    [DataRow(WiFiConnectionStatus.Success, null)]
    [DataRow(WiFiConnectionStatus.InvalidCredential, ConnectionFailReason.InvalidPassword)]
    [DataRow(WiFiConnectionStatus.Timeout, ConnectionFailReason.Timeout)]
    [DataRow(WiFiConnectionStatus.NetworkNotAvailable, ConnectionFailReason.NetworkUnavailable)]
    [DataRow(WiFiConnectionStatus.UnsupportedAuthenticationProtocol, ConnectionFailReason.UnsupportedAuthentication)]
    [DataRow(WiFiConnectionStatus.UnspecifiedFailure, ConnectionFailReason.Unknown)]
    public void Native_connection_result_mapping(WiFiConnectionStatus value, ConnectionFailReason? expected)
    {
        Assert.AreEqual(expected, NetworkBackend.MapConnectionResult(value));
    }

    [TestMethod]
    [DataRow(NetworkAuthenticationType.Open80211, NetworkEncryptionType.None, "open", "none", WifiSecurityType.Open)]
    [DataRow(NetworkAuthenticationType.Open80211, NetworkEncryptionType.Wep, "open", "WEP", WifiSecurityType.StaticWep)]
    [DataRow(NetworkAuthenticationType.Open80211, NetworkEncryptionType.Wep40, "open", "WEP",
        WifiSecurityType.StaticWep)]
    [DataRow(NetworkAuthenticationType.Open80211, NetworkEncryptionType.Wep104, "open", "WEP",
        WifiSecurityType.StaticWep)]
    [DataRow(NetworkAuthenticationType.None, NetworkEncryptionType.Wep, "open", "none", WifiSecurityType.Open)]
    [DataRow(NetworkAuthenticationType.SharedKey80211, NetworkEncryptionType.None, "shared", "none",
        WifiSecurityType.StaticWep)]
    [DataRow(NetworkAuthenticationType.Wpa, NetworkEncryptionType.None, "WPA", "TKIP", WifiSecurityType.WpaEap)]
    [DataRow(NetworkAuthenticationType.WpaPsk, NetworkEncryptionType.None, "WPAPSK", "TKIP", WifiSecurityType.WpaPsk)]
    [DataRow(NetworkAuthenticationType.Rsna, NetworkEncryptionType.None, "WPA2", "AES", WifiSecurityType.Wpa2Eap)]
    [DataRow(NetworkAuthenticationType.Wpa3Enterprise, NetworkEncryptionType.None, "WPA3ENT", "AES",
        WifiSecurityType.Wpa2Eap)]
    [DataRow(NetworkAuthenticationType.RsnaPsk, NetworkEncryptionType.None, "WPA2PSK", "aes", WifiSecurityType.Wpa2Psk)]
    [DataRow(NetworkAuthenticationType.Wpa3Enterprise192Bits, NetworkEncryptionType.None, "WPA3ENT192", "GCMP256",
        WifiSecurityType.Wpa3SuiteB192)]
    [DataRow(NetworkAuthenticationType.Wpa3Sae, NetworkEncryptionType.None, "WPA3SAE", "AES", WifiSecurityType.Sae)]
    [DataRow(NetworkAuthenticationType.Wpa3Sae, NetworkEncryptionType.None, "WPA3", "AES", WifiSecurityType.Sae)]
    [DataRow(NetworkAuthenticationType.Owe, NetworkEncryptionType.None, "OWE", "AES", WifiSecurityType.Owe)]
    [DataRow((NetworkAuthenticationType)999, NetworkEncryptionType.None, "other", "AES", WifiSecurityType.Unknown)]
    public void One_security_map_reads_a_scan_and_a_profile_the_same_way(
        NetworkAuthenticationType authentication, NetworkEncryptionType encryption, string profileAuthentication,
        string profileEncryption, WifiSecurityType expected)
    {
        Assert.AreEqual(expected, WifiFields.Security(authentication, encryption));
        Assert.AreEqual(expected, WifiFields.Security(profileAuthentication, profileEncryption));
    }

    [TestMethod]
    public void Strength_and_profile_id_have_one_rule()
    {
        Assert.AreEqual(0, WifiFields.Bars(0));
        Assert.AreEqual(1, WifiFields.Bars(5));
        Assert.IsNull(WifiFields.Bars(null));
        Assert.AreEqual(0.4, WifiFields.Bars(2)!.Value, 0.000005);
        Assert.AreEqual(0, WifiFields.Rssi(-100));
        Assert.AreEqual(0.5, WifiFields.Rssi(-75));
        Assert.AreEqual(1, WifiFields.Rssi(-50));
        Assert.AreEqual(0, WifiFields.Rssi(-120));
        Assert.AreEqual(1, WifiFields.Rssi(-20));

        var adapter = Guid.Parse("0b0f2811-87b5-48d6-9d7a-7a26ae12ff33");
        Assert.AreEqual("0b0f2811-87b5-48d6-9d7a-7a26ae12ff33:Office", WifiFields.Id(adapter, "Office"));
    }

    [TestMethod]
    public void Wifi_radios_and_adapters_are_reread_when_the_set_of_wifi_devices_changes()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        Assert.IsTrue(NetworkBackend.WifiDevicesChanged(null, new HashSet<Guid>()));
        Assert.IsFalse(NetworkBackend.WifiDevicesChanged(new HashSet<Guid> { a, b }, new HashSet<Guid> { b, a }));
        Assert.IsTrue(NetworkBackend.WifiDevicesChanged(new HashSet<Guid> { a }, new HashSet<Guid> { a, b }));
        Assert.IsTrue(NetworkBackend.WifiDevicesChanged(new HashSet<Guid> { a, b }, new HashSet<Guid> { a }));
    }
}
