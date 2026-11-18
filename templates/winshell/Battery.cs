namespace MyShell;

public sealed class Battery : Widget
{
    public override Element Render()
    {
        var battery = UseObservable(Shell.Power.DisplayDevice);
        return TextBlock($"{battery.Percentage:P0}")
            .Margin(8, 0, 8, 0)
            .IsVisible(battery.IsPresent)
            .AutomationName("Battery");
    }
}
