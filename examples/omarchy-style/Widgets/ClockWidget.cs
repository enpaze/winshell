using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OmarchyStyle;

public sealed class ClockWidget : Widget
{
    public override Element Render()
    {
        var now = UseNow(TimeSpan.FromSeconds(1));
        var id = Setting("timezone", "").Trim();
        var zone = id.Length == 0 ? null : TimeZoneInfo.FindSystemTimeZoneById(id);
        var time = zone is null ? now : TimeZoneInfo.ConvertTime(now, zone);
        var vertical = Orientation == Orientation.Vertical;
        var text = vertical ? $"{time:HH}\n{time:mm}" : $"{time:dddd HH:mm}";
        var tip = zone is null ? $"{time:D}" : $"{time:D} · {zone.Id}";
        var block = TextBlock(text)
            .ToolTip(tip)
            .AutomationId("clock")
            .Set(label => label.TextAlignment = TextAlignment.Center);
        return vertical
            ? block.Padding(0).HAlign(HorizontalAlignment.Center)
            : block.Padding(8.5, 0, 8.5, 0);
    }
}
