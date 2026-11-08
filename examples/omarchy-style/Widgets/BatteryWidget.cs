using System.Globalization;
using Microsoft.UI.Xaml;
using Winshell;
using Winshell.Power;

namespace OmarchyStyle;

/// <summary>The battery, when the PC has one.</summary>
public sealed class BatteryWidget(WidgetContext context) : StatusWidget(context, "ms-settings:powersleep")
{
    protected override void Show()
    {
        var power = Read(Shell.Power);
        var battery = Read(power.DisplayDevice);
        Visibility = battery.IsPresent ? Visibility.Visible : Visibility.Collapsed;
        if (!battery.IsPresent)
            return;

        var state = power.OnBattery ? "On battery"
            : battery.State == PowerDeviceState.FullyCharged ? "Fully charged"
            : battery.State == PowerDeviceState.NotCharging ? "Not charging"
            : "Charging";
        var percent = Math.Round(battery.Percentage * 100).ToString(CultureInfo.CurrentCulture);
        Bar.Show(this, Glyphs.Power(battery, power.OnBattery), percent + "% · " + state);
    }
}
