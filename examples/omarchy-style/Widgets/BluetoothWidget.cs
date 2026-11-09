using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace OmarchyStyle;

/// <summary>Bluetooth: off, on, or the names of the devices connected to it. Hidden with no adapter.</summary>
public sealed class BluetoothWidget : Button
{
    public BluetoothWidget(WidgetContext context)
    {
        var bluetooth = context.Shell.Bluetooth;
        this.Watch(() =>
        {
            var adapter = bluetooth.DefaultAdapter;
            Visibility = adapter is null ? Visibility.Collapsed : Visibility.Visible;
            if (adapter is null)
                return;
            var connected = bluetooth.Devices.Where(device => device.Connected).Select(device => device.Name).ToList();
            var (glyph, tip) = !adapter.Enabled ? (Glyphs.BluetoothOff, "Bluetooth off")
                : connected.Count > 0 ? (Glyphs.BluetoothConnected, string.Join('\n', connected))
                : (Glyphs.BluetoothOn, "Bluetooth on");
            Content = glyph;
            ToolTipService.SetToolTip(this, tip);
            AutomationProperties.SetName(this, tip);
        });
        Click += (_, _) => context.Shell.Open("ms-settings:bluetooth");
    }
}
