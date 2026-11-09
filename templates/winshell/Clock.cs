using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace MyShell;

public sealed class Clock : UserControl
{
    public Clock(WidgetContext context)
    {
        var text = new TextBlock { Text = $"{DateTime.Now:t}" };
        Content = text;
        // Watch follows the shell's services, not the time, so a timer sets the text each second while it shows.
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) => text.Text = $"{DateTime.Now:t}";
        Loaded += (_, _) => timer.Start();
        Unloaded += (_, _) => timer.Stop();
    }
}
