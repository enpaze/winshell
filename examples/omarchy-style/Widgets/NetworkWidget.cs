using System.Globalization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winshell;
using Winshell.Network;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

/// <summary>The connection in use, wired before Wi-Fi, in the alert color when it can't reach the internet.</summary>
public sealed class NetworkWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        var observe = UseRef(new Observe());
        _ = UseObservable(observe.Current);
        var networking = UseObservable(Props.Shell.Networking);
        var devices = UseObservable(networking.Devices);
        var profiles = UseObservable(networking.Profiles);
        var observers = new List<Element>();
        foreach (var item in devices)
            observers.Add(Observe.Of(item, observe.Current, BarLayout.Key(item),
                () => $"{item.Connected}\n{item.Name}\n{item.Type}\n{item.Id}"));
        foreach (var item in profiles)
            observers.Add(Observe.Of(item, observe.Current, BarLayout.Key(item), () =>
                string.Create(CultureInfo.InvariantCulture,
                    $"{item.Name}\n{item.AdapterId}\n{item.SignalStrength}")));

        var device = devices.Where(item => item.Connected)
            .OrderBy(item => item.Type == DeviceType.Wired ? 0 : 1)
            .FirstOrDefault();
        var profile = device is null ? null : profiles.FirstOrDefault(item => item.AdapterId == device.Id);
        var portal = networking.Connectivity == NetworkConnectivity.CaptivePortal;
        var restricted = device is not null && (portal || networking.Connectivity == NetworkConnectivity.LocalAccess);
        var name = profile?.Name ?? device?.Name;
        var tip = name is null ? "Disconnected"
            : portal ? name + ": sign in to the network"
            : restricted ? name + ": no internet"
            : name;
        var alert = restricted && Props.Style is { } style ? style.Alert : (Windows.UI.Color?)null;
        var button = Button(Glyphs.Network(device?.Type ?? DeviceType.None, profile?.SignalStrength ?? 0, restricted),
                () => Props.Shell.Open("ms-settings:network-status"))
            .ToolTip(tip)
            .AutomationName(tip)
            .Set(control =>
            {
                // The style's alert color. Clearing it returns the glyph to the style's foreground.
                if (alert is { } color)
                    control.Foreground = new SolidColorBrush(color);
                else
                    control.ClearValue(Control.ForegroundProperty);
            });
        return Observe.Show(button, HorizontalAlignment.Left, [.. observers]);
    }
}
