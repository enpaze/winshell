using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace MyShell;

public sealed class Battery : UserControl
{
    public Battery(WidgetContext context)
    {
        var text = new TextBlock { Margin = new Thickness(8, 0, 8, 0) };
        Content = text;
        var battery = context.Shell.Power.DisplayDevice;
        // Each Watch runs its block now, and again whenever what the block read changes.
        this.Watch(() => text.Text = $"{battery.Percentage:P0}");
        this.Watch(() => Visibility = battery.IsPresent ? Visibility.Visible : Visibility.Collapsed);
    }
}
