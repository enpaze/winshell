using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace OmarchyStyle;

/// <summary>
/// The day and time, such as <c>Saturday 20:25</c>, in the Windows regional format. An optional <c>timezone</c> setting
/// names another zone, such as <c>UTC</c>; without one the clock shows local time. A vertical area shows the hours
/// above the minutes, such as <c>20</c> over <c>25</c>. The tooltip is the full date either way.
/// </summary>
public sealed partial class ClockWidget : IDisposable
{
    private readonly DispatcherQueueTimer _clock;
    private readonly TimeZoneInfo? _zone;
    private readonly bool _vertical;
    private bool _disposed;

    public ClockWidget(WidgetContext context)
    {
        InitializeComponent();
        _vertical = context.Orientation == Orientation.Vertical;
        if (_vertical)
        {
            Time.Padding = new Thickness(0);
            Time.TextAlignment = TextAlignment.Center;
            HorizontalAlignment = HorizontalAlignment.Center;
        }

        _zone = Zone(context);
        _clock = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _clock.IsRepeating = false;
        _clock.Tick += (_, _) => ShowTime();
        Loaded += (_, _) =>
        {
            if (_disposed)
                return;
            Time.Style = Bar.Resource<Style>(this, "PanelText");
            ShowTime();
        };
    }

    public void Dispose()
    {
        _disposed = true;
        _clock.Stop();
    }

    private static TimeZoneInfo? Zone(WidgetContext context)
    {
        if (!context.Settings.TryGetValue("timezone", out var value) || value is null)
            return null;
        if (value is not string id)
            throw new UnsupportedWidgetException("timezone must be a time zone id, such as \"UTC\".");
        id = id.Trim();
        return id.Length == 0 ? null : TimeZoneInfo.FindSystemTimeZoneById(id);
    }

    private void ShowTime()
    {
        if (_disposed)
            return;
        var now = _zone is null ? DateTime.Now : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);
        var culture = CultureInfo.CurrentCulture;
        Time.Text = _vertical
            ? now.ToString("HH", culture) + "\n" + now.ToString("mm", culture)
            : now.ToString("dddd HH:mm", culture);
        var tip = now.ToString("D", CultureInfo.CurrentCulture);
        if (_zone is not null)
            tip += " · " + _zone.Id;
        Bar.Tip(Time, tip);
        _clock.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
        _clock.Start();
    }
}
