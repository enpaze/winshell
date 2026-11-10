using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

/// <summary>Bluetooth: off, on, or the names of the devices connected to it. Hidden with no adapter.</summary>
public sealed class BluetoothWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        var bluetooth = UseObservableTree(Props.Shell.Bluetooth);
        var devices = bluetooth.Devices;
        var adapter = bluetooth.DefaultAdapter;
        var connected = devices.Where(device => device.Connected).Select(device => device.Name).ToList();
        var (glyph, tip) = adapter is null ? ("", "")
            : !adapter.Enabled ? (Glyphs.BluetoothOff, "Bluetooth off")
            : connected.Count > 0 ? (Glyphs.BluetoothConnected, string.Join('\n', connected))
            : (Glyphs.BluetoothOn, "Bluetooth on");
        return Button(glyph, () => Props.Shell.Open("ms-settings:bluetooth"))
            .ToolTip(tip)
            .AutomationName(tip)
            .IsVisible(adapter is not null)
            .HAlign(HorizontalAlignment.Left);
    }
}
