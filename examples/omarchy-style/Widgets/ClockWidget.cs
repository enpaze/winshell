using Microsoft.UI.Dispatching;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

/// <summary>
/// The day and time, such as <c>Saturday 20:25</c>, in the Windows regional format. An optional <c>timezone</c> setting
/// names another zone, such as <c>UTC</c>. A side bar shows the hours above the minutes. The tooltip is the date.
/// </summary>
public sealed class ClockWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        // The time is not a service. A timer renders again when the shown minute changes, so an open tooltip stays open.
        var (tick, setTick) = UseState(0);
        var seen = UseRef("");
        var generation = UseRef(0);
        var face = UseRef<Func<string>?>(null);
        UseEffect(() =>
        {
            var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            void Show()
            {
                var text = face.Current?.Invoke();
                if (text is null || text == seen.Current)
                    return;
                seen.Current = text;
                setTick(++generation.Current);
            }

            timer.Tick += (_, _) => Show();
            timer.Start();
            Show();
            return () => timer.Stop();
        }, false);

        var zone = Zone(Props);
        var vertical = Props.Orientation == Orientation.Vertical;
        var now = zone is null ? DateTime.Now : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        var text = Face(now, vertical);
        var tip = zone is null ? $"{now:D}" : $"{now:D} · {zone.Id}";
        face.Current = () =>
        {
            var currentZone = Zone(Props);
            var time = currentZone is null ? DateTime.Now : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, currentZone);
            return Face(time, Props.Orientation == Orientation.Vertical);
        };
        seen.Current = text;
        _ = tick;
        var block = TextBlock(text)
            .ToolTip(tip)
            .Set(label => label.TextAlignment = TextAlignment.Center);
        return vertical
            ? block.Padding(0).HAlign(HorizontalAlignment.Center)
            : block.Padding(8.5, 0, 8.5, 0);
    }

    private static string Face(DateTime time, bool vertical) =>
        vertical ? $"{time:HH}\n{time:mm}" : $"{time:dddd HH:mm}";

    private static TimeZoneInfo? Zone(WidgetContext context)
    {
        if (context.Settings.GetValueOrDefault("timezone") is not { } value)
            return null;
        if (value is not string id)
            throw new UnsupportedWidgetException("timezone must be a time zone id, such as \"UTC\".");
        return id.Trim().Length == 0 ? null : TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
    }
}
