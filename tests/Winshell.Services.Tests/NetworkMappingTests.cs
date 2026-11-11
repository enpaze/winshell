using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Winshell.Network;

namespace Winshell.Services.Tests;

public sealed class NetworkMappingTests
{
    [Theory]
    [InlineData(NetworkConnectivityLevel.None, NetworkConnectivity.None)]
    [InlineData(NetworkConnectivityLevel.LocalAccess, NetworkConnectivity.LocalAccess)]
    [InlineData(NetworkConnectivityLevel.ConstrainedInternetAccess, NetworkConnectivity.CaptivePortal)]
    [InlineData(NetworkConnectivityLevel.InternetAccess, NetworkConnectivity.InternetAccess)]
    public void Connectivity_mapping(NetworkConnectivityLevel value, NetworkConnectivity expected)
    {
        Assert.Equal(expected, NetworkBackend.MapConnectivity(value));
    }

    [Theory]
    [InlineData(null, AirplaneModeState.Unknown)]
    [InlineData(0, AirplaneModeState.Off)]
    [InlineData(1, AirplaneModeState.On)]
    [InlineData(42, AirplaneModeState.Unknown)]
    public void Airplane_mode_is_not_inferred_from_radios(object? value, AirplaneModeState expected)
    {
        Assert.Equal(expected, NetworkBackend.MapAirplaneMode(value));
    }

    [Theory]
    [InlineData(0x00000004u, WifiDeviceMode.Station)]
    [InlineData(0x00000008u, WifiDeviceMode.AccessPoint)]
    [InlineData(0x00000000u, WifiDeviceMode.Unknown)]
    [InlineData(0x00000001u, WifiDeviceMode.Unknown)]
    [InlineData(0x00000002u, WifiDeviceMode.Unknown)]
    [InlineData(0x00000010u, WifiDeviceMode.Unknown)]
    [InlineData(0x00000020u, WifiDeviceMode.Unknown)]
    [InlineData(0x00000040u, WifiDeviceMode.Unknown)]
    [InlineData(0x40000000u, WifiDeviceMode.Unknown)]
    [InlineData(0x80000000u, WifiDeviceMode.Unknown)]
    public void Operation_mode_maps_extensible_station_and_access_point(uint operationMode, WifiDeviceMode expected)
    {
        Assert.Equal(expected, NetworkBackend.MapDeviceMode(operationMode));
    }

    [Fact]
    public void A_failed_operation_mode_query_is_unknown()
    {
        Assert.Equal(WifiDeviceMode.Unknown, NetworkBackend.MapDeviceMode(null));
    }

    [Theory]
    [InlineData(WiFiConnectionStatus.Success, null)]
    [InlineData(WiFiConnectionStatus.InvalidCredential, ConnectionFailReason.InvalidPassword)]
    [InlineData(WiFiConnectionStatus.Timeout, ConnectionFailReason.Timeout)]
    [InlineData(WiFiConnectionStatus.NetworkNotAvailable, ConnectionFailReason.NetworkUnavailable)]
    [InlineData(WiFiConnectionStatus.UnsupportedAuthenticationProtocol, ConnectionFailReason.UnsupportedAuthentication)]
    [InlineData(WiFiConnectionStatus.UnspecifiedFailure, ConnectionFailReason.Unknown)]
    public void Native_connection_result_mapping(WiFiConnectionStatus value, ConnectionFailReason? expected)
    {
        Assert.Equal(expected, NetworkBackend.MapConnectionResult(value));
    }

    [Theory]
    [InlineData(NetworkAuthenticationType.Open80211, NetworkEncryptionType.None, "open", "none", WifiSecurityType.Open)]
    [InlineData(NetworkAuthenticationType.Open80211, NetworkEncryptionType.Wep, "open", "WEP", WifiSecurityType.StaticWep)]
    [InlineData(NetworkAuthenticationType.Open80211, NetworkEncryptionType.Wep40, "open", "WEP",
        WifiSecurityType.StaticWep)]
    [InlineData(NetworkAuthenticationType.Open80211, NetworkEncryptionType.Wep104, "open", "WEP",
        WifiSecurityType.StaticWep)]
    [InlineData(NetworkAuthenticationType.None, NetworkEncryptionType.Wep, "open", "none", WifiSecurityType.Open)]
    [InlineData(NetworkAuthenticationType.SharedKey80211, NetworkEncryptionType.None, "shared", "none",
        WifiSecurityType.StaticWep)]
    [InlineData(NetworkAuthenticationType.Wpa, NetworkEncryptionType.None, "WPA", "TKIP", WifiSecurityType.WpaEap)]
    [InlineData(NetworkAuthenticationType.WpaPsk, NetworkEncryptionType.None, "WPAPSK", "TKIP", WifiSecurityType.WpaPsk)]
    [InlineData(NetworkAuthenticationType.Rsna, NetworkEncryptionType.None, "WPA2", "AES", WifiSecurityType.Wpa2Eap)]
    [InlineData(NetworkAuthenticationType.Wpa3Enterprise, NetworkEncryptionType.None, "WPA3ENT", "AES",
        WifiSecurityType.Wpa2Eap)]
    [InlineData(NetworkAuthenticationType.RsnaPsk, NetworkEncryptionType.None, "WPA2PSK", "aes", WifiSecurityType.Wpa2Psk)]
    [InlineData(NetworkAuthenticationType.Wpa3Enterprise192Bits, NetworkEncryptionType.None, "WPA3ENT192", "GCMP256",
        WifiSecurityType.Wpa3SuiteB192)]
    [InlineData(NetworkAuthenticationType.Wpa3Sae, NetworkEncryptionType.None, "WPA3SAE", "AES", WifiSecurityType.Sae)]
    [InlineData(NetworkAuthenticationType.Wpa3Sae, NetworkEncryptionType.None, "WPA3", "AES", WifiSecurityType.Sae)]
    [InlineData(NetworkAuthenticationType.Owe, NetworkEncryptionType.None, "OWE", "AES", WifiSecurityType.Owe)]
    [InlineData((NetworkAuthenticationType)999, NetworkEncryptionType.None, "other", "AES", WifiSecurityType.Unknown)]
    public void One_security_map_reads_a_scan_and_a_profile_the_same_way(
        NetworkAuthenticationType authentication, NetworkEncryptionType encryption, string profileAuthentication,
        string profileEncryption, WifiSecurityType expected)
    {
        Assert.Equal(expected, WifiFields.Security(authentication, encryption));
        Assert.Equal(expected, WifiFields.Security(profileAuthentication, profileEncryption));
    }

    [Fact]
    public void Strength_and_profile_id_have_one_rule()
    {
        Assert.Equal(0, WifiFields.Bars(0));
        Assert.Equal(1, WifiFields.Bars(5));
        Assert.Null(WifiFields.Bars(null));
        Assert.Equal(0.4, WifiFields.Bars(2)!.Value, precision: 5);
        Assert.Equal(0, WifiFields.Rssi(-100));
        Assert.Equal(0.5, WifiFields.Rssi(-75));
        Assert.Equal(1, WifiFields.Rssi(-50));
        Assert.Equal(0, WifiFields.Rssi(-120));
        Assert.Equal(1, WifiFields.Rssi(-20));

        var adapter = Guid.Parse("0b0f2811-87b5-48d6-9d7a-7a26ae12ff33");
        Assert.Equal("0b0f2811-87b5-48d6-9d7a-7a26ae12ff33:Office", WifiFields.Id(adapter, "Office"));
    }

    [Fact]
    public void Wifi_radios_and_adapters_are_reread_when_the_set_of_wifi_devices_changes()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        Assert.True(NetworkBackend.WifiDevicesChanged(null, new HashSet<Guid>()));
        Assert.False(NetworkBackend.WifiDevicesChanged(new HashSet<Guid> { a, b }, new HashSet<Guid> { b, a }));
        Assert.True(NetworkBackend.WifiDevicesChanged(new HashSet<Guid> { a }, new HashSet<Guid> { a, b }));
        Assert.True(NetworkBackend.WifiDevicesChanged(new HashSet<Guid> { a, b }, new HashSet<Guid> { a }));
    }
}
