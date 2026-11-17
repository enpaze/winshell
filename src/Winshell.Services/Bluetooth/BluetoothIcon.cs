namespace Winshell.Bluetooth;

internal readonly record struct BluetoothClassReport(
    ushort? Major,
    ushort? Minor,
    ushort? Appearance,
    bool ClassUnreadable = false,
    bool AppearanceUnreadable = false);

internal static class BluetoothIcon
{
    internal const string MajorKey = "System.Devices.Aep.Bluetooth.Cod.Major";
    internal const string MinorKey = "System.Devices.Aep.Bluetooth.Cod.Minor";
    internal const string AppearanceKey = "System.Devices.Aep.Bluetooth.Le.Appearance";
    internal const string CategoryKey = "System.Devices.Aep.Bluetooth.Le.Appearance.Category";
    internal const string SubcategoryKey = "System.Devices.Aep.Bluetooth.Le.Appearance.Subcategory";

    internal static string FromReports(IEnumerable<BluetoothClassReport> reports)
    {
        ushort? major = null;
        ushort? minor = null;
        ushort? appearance = null;
        var classUnreadable = false;
        var appearanceUnreadable = false;
        foreach (var report in reports)
        {
            if (major is null && report.Major is { } found && (found != 0 || (report.Minor ?? 0) != 0))
            {
                major = found;
                minor = report.Minor;
            }

            if (report.ClassUnreadable)
                classUnreadable = true;
            if (appearance is null && report.Appearance is { } foundAppearance)
                appearance = foundAppearance;
            if (report.AppearanceUnreadable)
                appearanceUnreadable = true;
        }

        return Name(major, minor, appearance, major is null && classUnreadable,
            appearance is null && appearanceUnreadable);
    }

    internal static string Name(ushort? major, ushort? minor, ushort? appearance, bool classUnreadable = false,
        bool appearanceUnreadable = false)
    {
        if (classUnreadable)
            return "";
        if (major is { } majorCode && (majorCode != 0 || (minor ?? 0) != 0))
            return ClassName(majorCode, minor ?? 0);
        if (appearanceUnreadable || appearance is not { } value || value == 0)
            return "";
        return AppearanceName(value);
    }

    internal static BluetoothClassReport Read(IReadOnlyDictionary<string, object> properties)
    {
        var major = ReadUInt16(properties, MajorKey);
        var minor = ReadUInt16(properties, MinorKey);
        var classUnreadable = major.Bad || (major.Value is not null && minor.Bad);
        var appearance = ReadAppearance(properties);
        return new BluetoothClassReport(classUnreadable ? null : major.Value, classUnreadable ? null : minor.Value,
            appearance.Value, classUnreadable, appearance.Bad);
    }

    private static (ushort? Value, bool Bad) ReadAppearance(IReadOnlyDictionary<string, object> properties)
    {
        var raw = ReadUInt16(properties, AppearanceKey);
        if (raw.Bad)
            return (null, true);
        if (raw.Value is { } value)
            return (value, false);
        var category = ReadUInt16(properties, CategoryKey);
        var subcategory = ReadUInt16(properties, SubcategoryKey);
        if (category.Bad || subcategory.Bad)
            return (null, true);
        if (category.Value is not { } code)
            return (null, false);
        var sub = subcategory.Value ?? 0;
        if (code > 0x3ff || sub > 0x3f)
            return (null, true);
        return ((ushort)((code << 6) | sub), false);
    }

    private static (ushort? Value, bool Bad) ReadUInt16(IReadOnlyDictionary<string, object> properties, string key)
    {
        var value = properties.GetValueOrDefault(key);
        if (value is null)
            return (null, false);
        return value is ushort number ? (number, false) : (null, true);
    }

    private static string ClassName(ushort major, ushort minor)
    {
        if (major > 0x1f || minor > 0x3f)
            return "";
        switch (major)
        {
            case 0x01:
                return "computer";
            case 0x02:
                return minor switch
                {
                    0x01 or 0x02 or 0x03 or 0x05 => "phone",
                    0x04 => "modem",
                    _ => ""
                };
            case 0x03:
                return "network-wireless";
            case 0x04:
                return minor switch
                {
                    0x01 or 0x02 => "audio-headset",
                    0x06 => "audio-headphones",
                    0x0b or 0x0c or 0x0d => "camera-video",
                    _ => "audio-card"
                };
            case 0x05:
                var kind = (minor >> 4) & 0x3;
                var detail = minor & 0x7;
                return kind switch
                {
                    0x00 when detail is 0x01 or 0x02 => "input-gaming",
                    0x01 => "input-keyboard",
                    0x02 => detail == 0x05 ? "input-tablet" : "input-mouse",
                    _ => ""
                };
            case 0x06:
                if ((minor & 0x20) != 0)
                    return "printer";
                if ((minor & 0x08) != 0)
                    return "camera-photo";
                return "";
            default:
                return "";
        }
    }

    private static string AppearanceName(ushort appearance)
    {
        var category = (appearance & 0xffc0) >> 6;
        if (category == 0x0f)
            return (appearance & 0x3f) switch
            {
                0x01 => "input-keyboard",
                0x02 => "input-mouse",
                0x03 or 0x04 => "input-gaming",
                0x05 => "input-tablet",
                0x08 => "scanner",
                _ => ""
            };
        return category switch
        {
            0x00 => "unknown",
            0x01 => "phone",
            0x02 => "computer",
            0x05 => "video-display",
            0x0a => "multimedia-player",
            0x0b => "scanner",
            _ => ""
        };
    }
}
