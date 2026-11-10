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
        var networking = UseObservableTree(Props.Shell.Networking);
        var device = networking.Devices.Where(item => item.Connected)
            .OrderBy(item => item.Type == DeviceType.Wired ? 0 : 1)
            .FirstOrDefault();
        var profile = device is null ? null : networking.Profiles.FirstOrDefault(item => item.AdapterId == device.Id);
        var portal = networking.Connectivity == NetworkConnectivity.CaptivePortal;
        var restricted = device is not null && (portal || networking.Connectivity == NetworkConnectivity.LocalAccess);
        var name = profile?.Name ?? device?.Name;
        var tip = name is null ? "Disconnected"
            : portal ? name + ": sign in to the network"
            : restricted ? name + ": no internet"
            : name;
        var alert = restricted && Props.Style is { } style ? style.Alert : (Windows.UI.Color?)null;
        return Button(Glyphs.Network(device?.Type ?? DeviceType.None, profile?.SignalStrength ?? 0, restricted),
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
            })
            .HAlign(HorizontalAlignment.Left);
    }
}
