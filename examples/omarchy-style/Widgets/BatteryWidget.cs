using Winshell.Power;

namespace OmarchyStyle;

public sealed class BatteryWidget : Widget
{
    public override Element Render()
    {
        var power = UseObservable(Shell.Power);
        var battery = UseObservable(power.DisplayDevice);
        var state = power.OnBattery ? "On battery" : battery.State switch
        {
            PowerDeviceState.FullyCharged => "Fully charged",
            PowerDeviceState.NotCharging => "Not charging",
            _ => "Charging"
        };
        var tip = $"{Math.Round(battery.Percentage * 100)}% · {state}";
        return Button(Glyphs.Power(battery, power.OnBattery), () => Shell.Open("ms-settings:powersleep"))
            .ToolTip(tip)
            .AutomationName(tip)
            .AutomationId("battery")
            .IsVisible(battery.IsPresent);
    }
}
