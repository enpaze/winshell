using Microsoft.UI.Xaml;

namespace OmarchyStyle;

public sealed class BluetoothWidget : Widget
{
    public override Element Render()
    {
        var bluetooth = UseObservableTree(Shell.Bluetooth);
        var devices = bluetooth.Devices;
        var adapter = bluetooth.DefaultAdapter;
        var connected = devices.Where(device => device.Connected).Select(device => device.Name).ToList();
        var (glyph, tip) = adapter is null ? ("", "")
            : !adapter.Enabled ? (Glyphs.BluetoothOff, "Bluetooth off")
            : connected.Count > 0 ? (Glyphs.BluetoothConnected, string.Join('\n', connected))
            : (Glyphs.BluetoothOn, "Bluetooth on");
        return Button(glyph, () => Shell.Open("ms-settings:bluetooth"))
            .ToolTip(tip)
            .AutomationName(tip)
            .AutomationId("bluetooth")
            .IsVisible(adapter is not null)
            .HAlign(HorizontalAlignment.Left);
    }
}
