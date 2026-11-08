namespace Winshell.Network;

/// <summary>What a connection can reach.</summary>
public enum NetworkConnectivity
{
    /// <summary>Not read yet, or the last read failed.</summary>
    [PublicAPI] Unknown,

    /// <summary>No connection.</summary>
    [PublicAPI] None,

    /// <summary>Limited internet access, usually until someone signs in on a captive portal page.</summary>
    [PublicAPI] CaptivePortal,

    /// <summary>The local network only, without the internet.</summary>
    [PublicAPI] LocalAccess,

    /// <summary>The internet.</summary>
    [PublicAPI] InternetAccess
}

/// <summary>How a connection's data is charged, as Windows reports it.</summary>
public enum NetworkCost
{
    /// <summary>Windows does not know the cost.</summary>
    [PublicAPI] Unknown,

    /// <summary>Data is not limited.</summary>
    [PublicAPI] Unrestricted,

    /// <summary>Data is free up to a limit.</summary>
    [PublicAPI] Fixed,

    /// <summary>Data is charged by the byte.</summary>
    [PublicAPI] Variable
}

/// <summary>Whether Windows lets Winshell scan and connect Wi-Fi networks.</summary>
public enum NetworkAccessStatus
{
    /// <summary>Not asked yet, or the last request failed.</summary>
    [PublicAPI] Unspecified,

    /// <summary>Windows allows scanning and connecting.</summary>
    [PublicAPI] Allowed,

    /// <summary>The user denied access.</summary>
    [PublicAPI] DeniedByUser,

    /// <summary>Windows denied access, or refused a scan.</summary>
    [PublicAPI] DeniedBySystem
}

/// <summary>The state of the PC's Wi-Fi radios, taken together.</summary>
public enum WifiRadioState
{
    /// <summary>No Wi-Fi radio was found, Windows would not list them, or none reports its state.</summary>
    [PublicAPI] Unknown,

    /// <summary>At least one Wi-Fi radio is on.</summary>
    [PublicAPI] On,

    /// <summary>No Wi-Fi radio is on, and at least one can be turned on.</summary>
    [PublicAPI] Off,

    /// <summary>
    /// No Wi-Fi radio is on or off, and at least one is disabled, as by a hardware switch, so software can't turn one on.
    /// </summary>
    [PublicAPI] Disabled
}

/// <summary>Whether airplane mode is on.</summary>
public enum AirplaneModeState
{
    /// <summary>Windows has no airplane mode setting, or it could not be read.</summary>
    [PublicAPI] Unknown,

    /// <summary>Airplane mode is off.</summary>
    [PublicAPI] Off,

    /// <summary>Airplane mode is on.</summary>
    [PublicAPI] On
}

/// <summary>How far an adapter, or a network on it, is in connecting.</summary>
public enum ConnectionState
{
    /// <summary>Windows reported a state that is none of the others.</summary>
    [PublicAPI] Unknown,

    /// <summary>The adapter is finding, joining or signing in to the network.</summary>
    [PublicAPI] Connecting,

    /// <summary>The adapter is connected.</summary>
    [PublicAPI] Connected,

    /// <summary>The adapter is leaving the network.</summary>
    [PublicAPI] Disconnecting,

    /// <summary>The adapter is not connected.</summary>
    [PublicAPI] Disconnected
}

/// <summary>
/// Why connecting to a Wi-Fi network failed. <see cref="Network.Error"/> holds the value's name after a failed
/// <see cref="Network.Connect"/> or <see cref="WifiNetwork.ConnectWithPassword"/>.
/// </summary>
public enum ConnectionFailReason
{
    /// <summary>A failure that is none of the others.</summary>
    [PublicAPI] Unknown,

    /// <summary>The network refused the password or the saved credentials.</summary>
    [PublicAPI] InvalidPassword,

    /// <summary>The network did not answer in time.</summary>
    [PublicAPI] Timeout,

    /// <summary>The network is not in the last scan, or Windows has not given access to the adapter.</summary>
    [PublicAPI] NetworkUnavailable,

    /// <summary>The network signs in with a method Windows can't use here.</summary>
    [PublicAPI] UnsupportedAuthentication
}

/// <summary>The kind of a network adapter.</summary>
public enum DeviceType
{
    /// <summary>Neither Wi-Fi nor Ethernet. No listed device has this type.</summary>
    [PublicAPI] None,

    /// <summary>A Wi-Fi adapter.</summary>
    [PublicAPI] Wifi,

    /// <summary>An Ethernet adapter.</summary>
    [PublicAPI] Wired
}

/// <summary>How a Wi-Fi network or a saved profile is secured.</summary>
public enum WifiSecurityType
{
    /// <summary>WPA3-Enterprise in 192-bit mode.</summary>
    [PublicAPI] Wpa3SuiteB192,

    /// <summary>WPA3-Personal, which signs in with SAE.</summary>
    [PublicAPI] Sae,

    /// <summary>WPA2-Enterprise, or WPA3-Enterprise without 192-bit mode.</summary>
    [PublicAPI] Wpa2Eap,

    /// <summary>WPA2-Personal, with a password.</summary>
    [PublicAPI] Wpa2Psk,

    /// <summary>WPA-Enterprise.</summary>
    [PublicAPI] WpaEap,

    /// <summary>WPA-Personal, with a password.</summary>
    [PublicAPI] WpaPsk,

    /// <summary>WEP, with a fixed key.</summary>
    [PublicAPI] StaticWep,

    /// <summary>Enhanced Open: encrypted, with no password.</summary>
    [PublicAPI] Owe,

    /// <summary>Open: no password and no encryption.</summary>
    [PublicAPI] Open,

    /// <summary>Windows did not report the security, or it is none of the others.</summary>
    [PublicAPI] Unknown
}

/// <summary>The 802.11 mode of a <see cref="WifiDevice"/>. Windows never reports <see cref="Mesh"/> or <see cref="AdHoc"/>.</summary>
public enum WifiDeviceMode
{
    /// <summary>Windows reported no mode, or a mode that is none of the others.</summary>
    [PublicAPI] Unknown,

    /// <summary>The adapter hosts a network, as Mobile hotspot does.</summary>
    [PublicAPI] AccessPoint,

    /// <summary>An 802.11s mesh. Never reported.</summary>
    [PublicAPI] Mesh,

    /// <summary>The adapter joins networks as a client.</summary>
    [PublicAPI] Station,

    /// <summary>A peer-to-peer ad hoc network. Never reported.</summary>
    [PublicAPI] AdHoc
}
