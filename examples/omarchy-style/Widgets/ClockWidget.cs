using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace OmarchyStyle;

/// <summary>
/// The day and time, such as <c>Saturday 20:25</c>, in the Windows regional format. An optional <c>timezone</c> setting
/// names another zone, such as <c>UTC</c>. A side bar shows the hours above the minutes. The tooltip is the date.
/// </summary>
public sealed class ClockWidget : UserControl
{
    public ClockWidget(WidgetContext context)
    {
        var zone = Zone(context);
        var vertical = context.Orientation == Orientation.Vertical;
        var text = new TextBlock
        {
            Padding = vertical ? new Thickness(0) : new Thickness(8.5, 0, 8.5, 0),
            TextAlignment = TextAlignment.Center
        };
        Content = text;
        if (vertical)
            HorizontalAlignment = HorizontalAlignment.Center;

        void Show()
        {
            var now = zone is null ? DateTime.Now : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
            text.Text = vertical ? $"{now:HH}\n{now:mm}" : $"{now:dddd HH:mm}";
            ToolTipService.SetToolTip(text, zone is null ? $"{now:D}" : $"{now:D} · {zone.Id}");
        }

        // Watch follows the shell's services, not the time, so a timer shows the time each second while it's loaded.
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) => Show();
        Loaded += (_, _) => timer.Start();
        Unloaded += (_, _) => timer.Stop();
        Show();
    }

    private static TimeZoneInfo? Zone(WidgetContext context)
    {
        if (context.Settings.GetValueOrDefault("timezone") is not { } value)
            return null;
        if (value is not string id)
            throw new UnsupportedWidgetException("timezone must be a time zone id, such as \"UTC\".");
        return id.Trim().Length == 0 ? null : TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
    }
}
