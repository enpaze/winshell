using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Winshell;
using Winshell.Power;

namespace OmarchyStyle;

/// <summary>The battery, when the PC has one.</summary>
public sealed class BatteryWidget : Button
{
    public BatteryWidget(WidgetContext context)
    {
        var power = context.Shell.Power;
        this.Watch(() =>
        {
            var battery = power.DisplayDevice;
            Visibility = battery.IsPresent ? Visibility.Visible : Visibility.Collapsed;
            if (!battery.IsPresent)
                return;
            var state = power.OnBattery ? "On battery" : battery.State switch
            {
                PowerDeviceState.FullyCharged => "Fully charged",
                PowerDeviceState.NotCharging => "Not charging",
                _ => "Charging"
            };
            var tip = $"{Math.Round(battery.Percentage * 100)}% · {state}";
            Content = Glyphs.Power(battery, power.OnBattery);
            ToolTipService.SetToolTip(this, tip);
            AutomationProperties.SetName(this, tip);
        });
        Click += (_, _) => context.Shell.Open("ms-settings:powersleep");
    }
}
