using Winshell.Audio;
using Winshell.Network;
using Winshell.Power;

namespace OmarchyStyle;

internal static class Glyphs
{
    public const string Windows = "\U000F05B3";
    public const string TrayChevron = "\uF053";
    public const string Tiling = "\U000F14FB";
    public const string BluetoothOff = "\U000F00B2";
    public const string BluetoothConnected = "\U000F00B1";
    public const string BluetoothOn = "\U000F00AF";
    public const string Screen = "\U000F0379";
    public const string Screens = "\U000F037A";
    private const string WifiEmpty = "\U000F092F";
    private const string WifiWeak = "\U000F091F";
    private const string WifiFair = "\U000F0922";
    private const string WifiGood = "\U000F0925";
    private const string WifiFull = "\U000F0928";
    private const string WifiRestricted = "\U000F0929";
    private const string EthernetOff = "\U000F0202";
    private const string Ethernet = "\U000F0200";
    private const string Disconnected = "\U000F092E";
    private const string Headphones = "\U000F02CB";
    private const string Muted = "\uEEE8";
    private const string VolumeHigh = "\uF028";
    private const string VolumeLow = "\uF027";
    private const string VolumeQuiet = "\uF026";
    private const string Battery10 = "\U000F007A";
    private const string Battery20 = "\U000F007B";
    private const string Battery30 = "\U000F007C";
    private const string Battery40 = "\U000F007D";
    private const string Battery50 = "\U000F007E";
    private const string Battery60 = "\U000F007F";
    private const string Battery70 = "\U000F0080";
    private const string Battery80 = "\U000F0081";
    private const string Battery90 = "\U000F0082";
    private const string Battery100 = "\U000F0079";
    private const string Charging10 = "\U000F089C";
    private const string Charging20 = "\U000F0086";
    private const string Charging30 = "\U000F0087";
    private const string Charging40 = "\U000F0088";
    private const string Charging50 = "\U000F089D";
    private const string Charging60 = "\U000F0089";
    private const string Charging70 = "\U000F089E";
    private const string Charging80 = "\U000F008A";
    private const string Charging90 = "\U000F008B";
    private const string Charging100 = "\U000F0085";

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

    public static string Network(DeviceType kind, double signal, bool restricted)
    {
        return kind switch
        {
            DeviceType.Wifi => restricted ? WifiRestricted : Wifi[Math.Clamp((int)Math.Ceiling(signal * 5) - 1, 0, 4)],
            DeviceType.Wired => restricted ? EthernetOff : Ethernet,
            _ => Disconnected
        };
    }

    public static string Audio(AudioNode device)
    {
        var form = device.Properties.GetValueOrDefault(AudioProperty.FormFactor);
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

    public static string Power(PowerDevice battery, bool onBattery)
    {
        var tenth = Math.Clamp((int)Math.Floor(battery.Percentage * 10), 0, 9);
        return battery.State switch
        {
            PowerDeviceState.NotCharging => Battery[tenth],
            PowerDeviceState.FullyCharged => Charging[9],
            _ => onBattery ? Battery[tenth] : Charging[tenth]
        };
    }
}
