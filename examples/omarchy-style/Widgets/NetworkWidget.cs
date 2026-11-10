using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winshell;
using Winshell.Network;

namespace OmarchyStyle;

/// <summary>The connection in use, wired before Wi-Fi, in the alert color when it can't reach the internet.</summary>
public sealed class NetworkWidget : Button
{
    public NetworkWidget(WidgetContext context)
    {
        var networking = context.Shell.Networking;
        this.Watch(() =>
        {
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
            Content = Glyphs.Network(device?.Type ?? DeviceType.None, profile?.SignalStrength ?? 0, restricted);
            ToolTipService.SetToolTip(this, tip);
            AutomationProperties.SetName(this, tip);
            // The style's alert color. Clearing it returns the glyph to the style's foreground.
            if (restricted && context.Style is { } style)
                Foreground = new SolidColorBrush(style.Alert);
            else
                ClearValue(ForegroundProperty);
        });
        Click += (_, _) => context.Shell.Open("ms-settings:network-status");
    }
}
