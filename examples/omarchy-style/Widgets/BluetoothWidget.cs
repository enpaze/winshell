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
        var observe = UseRef(new Observe());
        _ = UseObservable(observe.Current);
        var bluetooth = UseObservable(Props.Shell.Bluetooth);
        var devices = UseObservable(bluetooth.Devices);
        var adapter = bluetooth.DefaultAdapter;
        var observers = new List<Element>();
        if (adapter is not null)
            observers.Add(Observe.Of(adapter, observe.Current, BarLayout.Key(adapter), () => adapter.Enabled ? "1" : "0"));
        foreach (var device in devices)
            observers.Add(Observe.Of(device, observe.Current, BarLayout.Key(device),
                () => device.Connected ? "1\n" + device.Name : "0\n" + device.Name));

        var connected = devices.Where(device => device.Connected).Select(device => device.Name).ToList();
        var (glyph, tip) = adapter is null ? ("", "")
            : !adapter.Enabled ? (Glyphs.BluetoothOff, "Bluetooth off")
            : connected.Count > 0 ? (Glyphs.BluetoothConnected, string.Join('\n', connected))
            : (Glyphs.BluetoothOn, "Bluetooth on");
        var button = Button(glyph, () => Props.Shell.Open("ms-settings:bluetooth"))
            .ToolTip(tip)
            .AutomationName(tip)
            .IsVisible(adapter is not null);
        return Observe.Show(button, HorizontalAlignment.Left, [.. observers]);
    }
}
