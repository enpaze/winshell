using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace MyShell;

/// <summary>
/// The charge, when the PC has a battery. Health support is its own flag, and a power mode Windows did not report
/// reads as unknown.
/// </summary>
public sealed class Battery : Component<WidgetContext>
{
    public override Element Render()
    {
        var battery = UseObservable(Props.Shell.Power.DisplayDevice);
        return TextBlock($"{battery.Percentage:P0}")
            .Margin(8, 0, 8, 0)
            .IsVisible(battery.IsPresent)
            .AutomationName("Battery");
    }
}
