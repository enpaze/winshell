using Windows.Devices.Enumeration;
using Winshell.Bluetooth;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class BluetoothIconTests
{
    private static readonly Guid Container = Guid.Parse("71886561-73c1-4d67-a4a0-5077c90859d0");

    [TestMethod]
    [DataRow(null, null)]
    [DataRow(0, 0.0)]
    [DataRow(42, .42)]
    [DataRow(100, 1.0)]
    [DataRow(255, null)]
    [DataRow(-1, null)]
    [DataRow("80", null)]
    public void Battery_rejects_missing_invalid_and_unknown_values(object? value, double? expected)
    {
        Assert.AreEqual(expected, BluetoothBackend.BatteryLevel(value));
    }

    [TestMethod]
    public void Battery_accepts_the_windows_numeric_property_types()
    {
        Assert.AreEqual(.5, BluetoothBackend.BatteryLevel((byte)50));
        Assert.AreEqual(.5, BluetoothBackend.BatteryLevel((ushort)50));
        Assert.AreEqual(.5, BluetoothBackend.BatteryLevel(50u));
        Assert.IsNull(BluetoothBackend.BatteryLevel(255u));
    }

    [TestMethod]
    [DataRow(DevicePairingResultStatus.Paired, null)]
    [DataRow(DevicePairingResultStatus.AlreadyPaired, null)]
    [DataRow(DevicePairingResultStatus.AuthenticationFailure, "AuthenticationFailure")]
    [DataRow(DevicePairingResultStatus.RejectedByHandler, "RejectedByHandler")]
    [DataRow(DevicePairingResultStatus.OperationAlreadyInProgress, "OperationAlreadyInProgress")]
    public void Pairing_result_mapping(DevicePairingResultStatus status, string? error)
    {
        Assert.AreEqual(error, BluetoothBackend.PairingError(status));
    }

    [TestMethod]
    [DataRow(DeviceUnpairingResultStatus.Unpaired, null)]
    [DataRow(DeviceUnpairingResultStatus.AlreadyUnpaired, null)]
    [DataRow(DeviceUnpairingResultStatus.AccessDenied, "AccessDenied")]
    [DataRow(DeviceUnpairingResultStatus.Failed, "Failed")]
    public void Unpairing_result_mapping(DeviceUnpairingResultStatus status, string? error)
    {
        Assert.AreEqual(error, BluetoothBackend.UnpairingError(status));
    }

    [TestMethod]
    [DataRow(0x01, 0, null, "computer")]
    [DataRow(0x01, 5, 1 << 6, "computer")]
    [DataRow(0x02, 0x01, null, "phone")]
    [DataRow(0x02, 0x02, null, "phone")]
    [DataRow(0x02, 0x03, null, "phone")]
    [DataRow(0x02, 0x05, null, "phone")]
    [DataRow(0x02, 0x04, null, "modem")]
    [DataRow(0x02, 0x00, 1 << 6, "")]
    [DataRow(0x03, 0, null, "network-wireless")]
    [DataRow(0x03, 7, null, "network-wireless")]
    [DataRow(0x04, 0x01, null, "audio-headset")]
    [DataRow(0x04, 0x02, null, "audio-headset")]
    [DataRow(0x04, 0x06, null, "audio-headphones")]
    [DataRow(0x04, 0x0b, null, "camera-video")]
    [DataRow(0x04, 0x0c, null, "camera-video")]
    [DataRow(0x04, 0x0d, null, "camera-video")]
    [DataRow(0x04, 0x00, null, "audio-card")]
    [DataRow(0x04, 0x03, null, "audio-card")]
    [DataRow(0x04, 0x0e, null, "audio-card")]
    [DataRow(0x04, 0x0f, null, "audio-card")]
    [DataRow(0x04, 0x12, null, "audio-card")]
    [DataRow(0x04, 0x06, 1 << 6, "audio-headphones")]
    [DataRow(0x05, 0x01, null, "input-gaming")]
    [DataRow(0x05, 0x02, null, "input-gaming")]
    [DataRow(0x05, 0x10, null, "input-keyboard")]
    [DataRow(0x05, 0x20, null, "input-mouse")]
    [DataRow(0x05, 0x25, null, "input-tablet")]
    [DataRow(0x05, 0x22, null, "input-mouse")]
    [DataRow(0x05, 0x30, null, "")]
    [DataRow(0x05, 0x03, null, "")]
    [DataRow(0x05, 0x00, null, "")]
    [DataRow(0x06, 0x20, null, "printer")]
    [DataRow(0x06, 0x08, null, "camera-photo")]
    [DataRow(0x06, 0x28, null, "printer")]
    [DataRow(0x06, 0x04, null, "")]
    [DataRow(0x07, 0x01, 1 << 6, "")]
    [DataRow(0x08, 0, null, "")]
    [DataRow(0x09, 0, null, "")]
    [DataRow(0x00, 1, 1 << 6, "")]
    [DataRow(0x1f, 0, null, "")]
    [DataRow(0x00, 0, null, "")]
    [DataRow(0x00, 0, 1 << 6, "phone")]
    [DataRow(32, 0, 1 << 6, "")]
    [DataRow(0x04, 64, 1 << 6, "")]
    [DataRow(null, null, null, "")]
    [DataRow(null, null, 0, "")]
    [DataRow(null, null, 1, "unknown")]
    [DataRow(null, null, 1 << 6, "phone")]
    [DataRow(null, null, 2 << 6, "computer")]
    [DataRow(null, null, 5 << 6, "video-display")]
    [DataRow(null, null, 10 << 6, "multimedia-player")]
    [DataRow(null, null, 11 << 6, "scanner")]
    [DataRow(null, null, (0x0f << 6) | 1, "input-keyboard")]
    [DataRow(null, null, (0x0f << 6) | 2, "input-mouse")]
    [DataRow(null, null, (0x0f << 6) | 3, "input-gaming")]
    [DataRow(null, null, (0x0f << 6) | 4, "input-gaming")]
    [DataRow(null, null, (0x0f << 6) | 5, "input-tablet")]
    [DataRow(null, null, (0x0f << 6) | 8, "scanner")]
    [DataRow(null, null, 0x0f << 6, "")]
    [DataRow(null, null, 3 << 6, "")]
    [DataRow(null, null, 0x2a << 6, "")]
    public void Icon_names_follow_the_BlueZ_class_and_appearance(int? major, int? minor, int? appearance,
        string expected)
    {
        Assert.AreEqual(expected, BluetoothIcon.Name(Code(major), Code(minor), Code(appearance)));
    }

    [TestMethod]
    public void Merged_endpoints_prefer_a_classic_class_over_an_appearance()
    {
        var headphones = new BluetoothClassReport(0x04, 0x06, null);
        var phone = new BluetoothClassReport(null, null, 1 << 6);
        Assert.AreEqual("audio-headphones", Assert.ContainsSingle(BluetoothBackend.MergeEndpoints([
            ClassEndpoint("le", true, phone), ClassEndpoint("classic", false, headphones)
        ])).Icon);

        Assert.AreEqual("computer", Assert.ContainsSingle(BluetoothBackend.MergeEndpoints([
            ClassEndpoint("headphones", false, headphones),
            ClassEndpoint("paired-computer", true, new BluetoothClassReport(0x01, 0, null))
        ])).Icon);

        Assert.AreEqual("computer", Assert.ContainsSingle(BluetoothBackend.MergeEndpoints([
            ClassEndpoint("b", true, headphones),
            ClassEndpoint("a", true, new BluetoothClassReport(0x01, 1, null))
        ])).Icon);

        Assert.AreEqual("phone",
            Assert.ContainsSingle(BluetoothBackend.MergeEndpoints([ClassEndpoint("only-le", true, phone)])).Icon);
        Assert.AreEqual("", Assert.ContainsSingle(BluetoothBackend.MergeEndpoints([
            ClassEndpoint("band", true, new BluetoothClassReport(0x07, 0x01, null)),
            ClassEndpoint("band-le", false, phone)
        ])).Icon);
        Assert.AreEqual("", Assert.ContainsSingle(BluetoothBackend.MergeEndpoints([
            ClassEndpoint("broken", true, new BluetoothClassReport(null, null, null, true)),
            ClassEndpoint("broken-le", false, phone)
        ])).Icon);
        Assert.AreEqual("phone", Assert.ContainsSingle(BluetoothBackend.MergeEndpoints([
            ClassEndpoint("zero", true, new BluetoothClassReport(0, 0, null)),
            ClassEndpoint("zero-le", false, phone)
        ])).Icon);
        Assert.AreEqual("audio-headset", Assert.ContainsSingle(BluetoothBackend.MergeEndpoints([
            ClassEndpoint("zero-first", true, new BluetoothClassReport(0, 0, null)),
            ClassEndpoint("headset", false, new BluetoothClassReport(0x04, 0x01, null))
        ])).Icon);
    }

    [TestMethod]
    public void A_class_property_of_the_wrong_type_is_empty_and_does_not_throw()
    {
        Assert.AreEqual("", IconProperties());
        Assert.AreEqual("video-display", IconProperties((BluetoothIcon.CategoryKey, (ushort)5)));
        Assert.AreEqual("input-keyboard", IconProperties(
            (BluetoothIcon.CategoryKey, (ushort)0x0f), (BluetoothIcon.SubcategoryKey, (ushort)1)));
        Assert.AreEqual("phone", IconProperties(
            (BluetoothIcon.AppearanceKey, (ushort)(1 << 6)), (BluetoothIcon.CategoryKey, (ushort)2)));
        Assert.AreEqual("", IconProperties(
            (BluetoothIcon.AppearanceKey, (ushort)0), (BluetoothIcon.CategoryKey, (ushort)1)));
        Assert.AreEqual("unknown", IconProperties(
            (BluetoothIcon.CategoryKey, (ushort)0), (BluetoothIcon.SubcategoryKey, (ushort)1)));
        Assert.AreEqual("", IconProperties((BluetoothIcon.CategoryKey, (ushort)0x400)));
        Assert.AreEqual("", IconProperties(
            (BluetoothIcon.CategoryKey, (ushort)5), (BluetoothIcon.SubcategoryKey, "no")));
        Assert.AreEqual("", IconProperties(
            (BluetoothIcon.MajorKey, "4"), (BluetoothIcon.AppearanceKey, (ushort)(1 << 6))));
        Assert.AreEqual("", IconProperties(
            (BluetoothIcon.MajorKey, (ushort)4), (BluetoothIcon.MinorKey, "6"),
            (BluetoothIcon.AppearanceKey, (ushort)(1 << 6))));
        Assert.AreEqual("audio-card", IconProperties((BluetoothIcon.MajorKey, (ushort)4)));
        Assert.AreEqual("", IconProperties(
            (BluetoothIcon.MajorKey, 4u), (BluetoothIcon.AppearanceKey, (ushort)(1 << 6))));
        Assert.AreEqual("", BluetoothIcon.Name(null, null, 1 << 6, true));
        Assert.AreEqual("", BluetoothIcon.Name(null, null, 1 << 6, appearanceUnreadable: true));
    }

    private static ushort? Code(int? value)
    {
        return value is not null ? (ushort)value.Value : null;
    }

    private static BluetoothEndpointInfo ClassEndpoint(string id, bool paired, BluetoothClassReport report)
    {
        return new BluetoothEndpointInfo(id, "aa:bb:cc:dd:ee:ff", "Device", false, paired, true, false, Container,
            report);
    }

    private static string IconProperties(params (string Key, object Value)[] properties)
    {
        return BluetoothIcon.FromReports([
            BluetoothIcon.Read(properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal))
        ]);
    }
}
