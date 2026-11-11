using Windows.Networking.Connectivity;

namespace Winshell.Network;

/// <summary>
/// The Wi-Fi facts a scan and a saved profile share: security, the profile id and strength. Both the backend and the
/// profile XML call this, so the two readings cannot grow a second map.
/// </summary>
internal static class WifiFields
{
    /// <summary>The adapter's id and the profile's name, joined by a colon.</summary>
    public static string Id(Guid adapter, string profileName)
    {
        return adapter + ":" + profileName;
    }

    /// <summary>Signal bars are 0 to 5. Null when Windows reports none.</summary>
    public static double? Bars(byte? bars)
    {
        return bars is { } value ? value / 5.0 : null;
    }

    /// <summary>RSSI in dBm, on the same 0 to 1 scale as <see cref="Bars"/>.</summary>
    public static double Rssi(double rssiInDbm)
    {
        return Math.Clamp(2 * (rssiInDbm + 100), 0, 100) / 100;
    }

    /// <summary>WinRT security settings. Null settings are unknown.</summary>
    public static WifiSecurityType Security(NetworkSecuritySettings? settings)
    {
        return settings is null
            ? WifiSecurityType.Unknown
            : Security(settings.NetworkAuthenticationType, settings.NetworkEncryptionType);
    }

    /// <summary>A scan's authentication and encryption, read through the profile-XML map.</summary>
    public static WifiSecurityType Security(NetworkAuthenticationType authentication, NetworkEncryptionType encryption)
    {
        var (auth, cipher) = authentication switch
        {
            NetworkAuthenticationType.Open80211 => ("open", Wep(encryption) ? "WEP" : "none"),
            NetworkAuthenticationType.None => ("open", "none"),
            NetworkAuthenticationType.SharedKey80211 => ("shared", ""),
            NetworkAuthenticationType.Wpa => ("WPA", ""),
            NetworkAuthenticationType.WpaPsk => ("WPAPSK", ""),
            NetworkAuthenticationType.Rsna => ("WPA2", ""),
            NetworkAuthenticationType.Wpa3Enterprise => ("WPA3ENT", ""),
            NetworkAuthenticationType.RsnaPsk => ("WPA2PSK", ""),
            NetworkAuthenticationType.Wpa3Enterprise192Bits => ("WPA3ENT192", ""),
            NetworkAuthenticationType.Wpa3Sae => ("WPA3SAE", ""),
            NetworkAuthenticationType.Owe => ("OWE", ""),
            _ => ((string?)null, (string?)null)
        };
        return auth is null ? WifiSecurityType.Unknown : Security(auth, cipher);
    }

    /// <summary>Profile-XML authentication and encryption. Encryption matters for open networks, which can be WEP.</summary>
    public static WifiSecurityType Security(string? authentication, string? encryption)
    {
        var auth = authentication ?? "";
        var cipher = encryption ?? "";
        if (auth.Equals("open", StringComparison.OrdinalIgnoreCase))
            return cipher.Equals("WEP", StringComparison.OrdinalIgnoreCase)
                ? WifiSecurityType.StaticWep
                : WifiSecurityType.Open;
        if (auth.Equals("shared", StringComparison.OrdinalIgnoreCase))
            return WifiSecurityType.StaticWep;
        if (auth.Equals("WPA", StringComparison.OrdinalIgnoreCase))
            return WifiSecurityType.WpaEap;
        if (auth.Equals("WPAPSK", StringComparison.OrdinalIgnoreCase))
            return WifiSecurityType.WpaPsk;
        if (auth.Equals("WPA2", StringComparison.OrdinalIgnoreCase) ||
            auth.Equals("WPA3ENT", StringComparison.OrdinalIgnoreCase))
            return WifiSecurityType.Wpa2Eap;
        if (auth.Equals("WPA2PSK", StringComparison.OrdinalIgnoreCase))
            return WifiSecurityType.Wpa2Psk;
        if (auth.Equals("WPA3ENT192", StringComparison.OrdinalIgnoreCase))
            return WifiSecurityType.Wpa3SuiteB192;
        if (auth.Equals("WPA3SAE", StringComparison.OrdinalIgnoreCase) ||
            auth.Equals("WPA3", StringComparison.OrdinalIgnoreCase))
            return WifiSecurityType.Sae;
        if (auth.Equals("OWE", StringComparison.OrdinalIgnoreCase))
            return WifiSecurityType.Owe;
        return WifiSecurityType.Unknown;
    }

    private static bool Wep(NetworkEncryptionType encryption)
    {
        return encryption is NetworkEncryptionType.Wep or NetworkEncryptionType.Wep40 or NetworkEncryptionType.Wep104;
    }
}
