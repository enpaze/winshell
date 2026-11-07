using Winshell.Audio;
using Winshell.Network;
using Winshell.Power;

namespace OmarchyStyle;

/// <summary>The Nerd Font glyphs Omarchy v4.5.0's bar shows, and when it shows each one.</summary>
internal static class Glyphs
{
    public const string Tiling = "\U000F14FB";

    public const string BluetoothOff = "\U000F00B2";
    public const string BluetoothConnected = "\U000F00B1";
    public const string BluetoothOn = "\U000F00AF";

    public const string Screen = "\U000F0379";
    public const string Screens = "\U000F037A";

    private static readonly string[] Wifi = ["\U000F092F", "\U000F091F", "\U000F0922", "\U000F0925", "\U000F0928"];
    private static readonly string[] Battery =
    [
        "\U000F007A", "\U000F007B", "\U000F007C", "\U000F007D", "\U000F007E",
        "\U000F007F", "\U000F0080", "\U000F0081", "\U000F0082", "\U000F0079"
    ];
    private static readonly string[] Charging =
    [
        "\U000F089C", "\U000F0086", "\U000F0087", "\U000F0088", "\U000F089D",
        "\U000F0089", "\U000F089E", "\U000F008A", "\U000F008B", "\U000F0085"
    ];

    /// <summary>Wi-Fi by signal, from 0 to 1, or Ethernet; with a slash while the connection can't reach the internet.</summary>
    public static string Network(DeviceType kind, double signal, bool restricted)
    {
        return kind switch
        {
            DeviceType.Wifi => restricted ? "\U000F0929" : Wifi[Math.Clamp((int)Math.Ceiling(signal * 100 / 20) - 1, 0, 4)],
            DeviceType.Wired => restricted ? "\U000F0202" : "\U000F0200",
            _ => "\U000F092E"
        };
    }

    /// <summary>Headphones, or a speaker by volume, with a cross when muted or silent.</summary>
    public static string Audio(AudioNode device)
    {
        var form = device.Properties.GetValueOrDefault("formFactor");
        if (form is "headphone" or "headset")
            return "\U000F02CB";
        if (device.Muted)
            return "\uEEE8";
        return device.Volume switch
        {
            >= 0.67 => "\uF028",
            >= 0.34 => "\uF027",
            > 0 => "\uF026",
            _ => "\uEEE8"
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
