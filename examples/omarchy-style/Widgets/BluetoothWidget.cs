using Microsoft.UI.Xaml;
using Winshell;

namespace OmarchyStyle;

/// <summary>Bluetooth: off, on, or the names of the devices connected to it.</summary>
public sealed class BluetoothWidget(WidgetContext context) : StatusWidget(context, "ms-settings:bluetooth")
{
    protected override void Show()
    {
        var bluetooth = Read(Shell.Bluetooth);
        var adapter = bluetooth.DefaultAdapter;
        if (adapter is null)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Read(adapter);
        var connected = Read(bluetooth.Devices).Where(device => Read(device).Connected).Select(device => device.Name)
            .ToList();
        Visibility = Visibility.Visible;
        if (!adapter.Enabled)
            Bar.Show(this, Glyphs.BluetoothOff, "Bluetooth off");
        else if (connected.Count > 0)
            Bar.Show(this, Glyphs.BluetoothConnected, string.Join('\n', connected));
        else
            Bar.Show(this, Glyphs.BluetoothOn, "Bluetooth on");
    }
}
