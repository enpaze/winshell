using Winshell.Audio;
using Winshell.Network;
using Winshell.Power;

namespace OmarchyStyle;

/// <summary>The Nerd Font glyphs Omarchy v4.5.0's bar shows, and when it shows each one.</summary>
internal static class Glyphs
{
    public const string Windows = "\U000F05B3"; // nf-md-microsoft_windows
    public const string TrayChevron = "\uF053"; // nf-fa-chevron_left
    public const string Tiling = "\U000F14FB"; // nf-md-square_rounded
    public const string BluetoothOff = "\U000F00B2"; // nf-md-bluetooth_off
    public const string BluetoothConnected = "\U000F00B1"; // nf-md-bluetooth_connect
    public const string BluetoothOn = "\U000F00AF"; // nf-md-bluetooth
    public const string Screen = "\U000F0379"; // nf-md-monitor
    public const string Screens = "\U000F037A"; // nf-md-monitor_multiple
    public const string WifiEmpty = "\U000F092F"; // nf-md-wifi_strength_outline
    public const string WifiWeak = "\U000F091F"; // nf-md-wifi_strength_1
    public const string WifiFair = "\U000F0922"; // nf-md-wifi_strength_2
    public const string WifiGood = "\U000F0925"; // nf-md-wifi_strength_3
    public const string WifiFull = "\U000F0928"; // nf-md-wifi_strength_4
    public const string WifiRestricted = "\U000F0929"; // nf-md-wifi_strength_4_alert
    public const string EthernetOff = "\U000F0202"; // nf-md-ethernet_cable_off
    public const string Ethernet = "\U000F0200"; // nf-md-ethernet
    public const string Disconnected = "\U000F092E"; // nf-md-wifi_strength_off_outline
    public const string Headphones = "\U000F02CB"; // nf-md-headphones
    public const string Muted = "\uEEE8"; // nf-fa-volume_xmark
    public const string VolumeHigh = "\uF028"; // nf-fa-volume_high
    public const string VolumeLow = "\uF027"; // nf-fa-volume_down
    public const string VolumeQuiet = "\uF026"; // nf-fa-volume_off
    public const string Battery10 = "\U000F007A"; // nf-md-battery_10
    public const string Battery20 = "\U000F007B"; // nf-md-battery_20
    public const string Battery30 = "\U000F007C"; // nf-md-battery_30
    public const string Battery40 = "\U000F007D"; // nf-md-battery_40
    public const string Battery50 = "\U000F007E"; // nf-md-battery_50
    public const string Battery60 = "\U000F007F"; // nf-md-battery_60
    public const string Battery70 = "\U000F0080"; // nf-md-battery_70
    public const string Battery80 = "\U000F0081"; // nf-md-battery_80
    public const string Battery90 = "\U000F0082"; // nf-md-battery_90
    public const string Battery100 = "\U000F0079"; // nf-md-battery
    public const string Charging10 = "\U000F089C"; // nf-md-battery_charging_10
    public const string Charging20 = "\U000F0086"; // nf-md-battery_charging_20
    public const string Charging30 = "\U000F0087"; // nf-md-battery_charging_30
    public const string Charging40 = "\U000F0088"; // nf-md-battery_charging_40
    public const string Charging50 = "\U000F089D"; // nf-md-battery_charging_50
    public const string Charging60 = "\U000F0089"; // nf-md-battery_charging_60
    public const string Charging70 = "\U000F089E"; // nf-md-battery_charging_70
    public const string Charging80 = "\U000F008A"; // nf-md-battery_charging_80
    public const string Charging90 = "\U000F008B"; // nf-md-battery_charging_90
    public const string Charging100 = "\U000F0085"; // nf-md-battery_charging_100

    private static readonly string[] Wifi = [WifiEmpty, WifiWeak, WifiFair, WifiGood, WifiFull];

    private static readonly string[] Battery =
    [
        Battery10, Battery20, Battery30, Battery40, Battery50,
        Battery60, Battery70, Battery80, Battery90, Battery100
    ];

    private static readonly string[] Charging =
    [
        Charging10, Charging20, Charging30, Charging40, Charging50,
        Charging60, Charging70, Charging80, Charging90, Charging100
    ];

    /// <summary>Wi-Fi by signal, from 0 to 1, or Ethernet; with a slash while the connection can't reach the internet.</summary>
    public static string Network(DeviceType kind, double signal, bool restricted)
    {
        return kind switch
        {
            DeviceType.Wifi => restricted ? WifiRestricted : Wifi[Math.Clamp((int)Math.Ceiling(signal * 5) - 1, 0, 4)],
            DeviceType.Wired => restricted ? EthernetOff : Ethernet,
            _ => Disconnected
        };
    }

    /// <summary>Headphones, or a speaker by volume, with a cross when muted or silent.</summary>
    public static string Audio(AudioNode device)
    {
        var form = device.Properties.GetValueOrDefault("formFactor");
        if (form is "headphone" or "headset")
            return Headphones;
        if (device.Muted)
            return Muted;
        return device.Volume switch
        {
            >= 0.67 => VolumeHigh,
            >= 0.34 => VolumeLow,
            > 0 => VolumeQuiet,
            _ => Muted
        };
    }

    /// <summary>A battery filled by tenths, with a bolt while it charges.</summary>
    public static string Power(PowerDevice battery, bool onBattery)
    {
        var tenth = Math.Clamp((int)Math.Floor(battery.Percentage * 10), 0, 9);
        return battery.State switch
        {
            // Plugged in and held below full, as Omarchy's charge threshold is.
            PowerDeviceState.NotCharging => Battery[tenth],
            PowerDeviceState.FullyCharged => Charging[9],
            _ => onBattery ? Battery[tenth] : Charging[tenth]
        };
    }
}
