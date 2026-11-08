using Winshell;
using Winshell.Network;

namespace OmarchyStyle;

/// <summary>The connection in use, wired before Wi-Fi, in the alert color when it cannot reach the internet.</summary>
public sealed class NetworkWidget(WidgetContext context) : StatusWidget(context, "ms-settings:network-status")
{
    protected override void Show()
    {
        var networking = Read(Shell.Networking);
        var device = Read(networking.Devices)
            .Where(item => Read(item).Connected)
            .OrderBy(item => item.Type == DeviceType.Wired ? 0 : 1)
            .FirstOrDefault();
        var profile = device is null
            ? null
            : Read(networking.Profiles).Select(Read)
                .FirstOrDefault(item => item.AdapterId == device.Id);
        var portal = networking.Connectivity == NetworkConnectivity.CaptivePortal;
        var restricted = device is not null && (portal || networking.Connectivity == NetworkConnectivity.LocalAccess);
        var name = profile?.Name ?? device?.Name;
        Bar.Show(
            this,
            Glyphs.Network(device?.Type ?? DeviceType.None, profile?.SignalStrength ?? 0, restricted),
            ToolTip(name, portal, restricted),
            restricted);
    }

    private static string ToolTip(string? name, bool portal, bool restricted)
    {
        if (name is null)
            return "Disconnected";
        if (portal)
            return name + ": sign in to the network";
        if (restricted)
            return name + ": no internet";
        return name;
    }
}
