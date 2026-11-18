using System.Collections.Immutable;
using Microsoft.UI.Xaml;

namespace FeatureMap;

public sealed record FeatureWindowProps(RowProps[] Services, RowProps Floating, Action OpenPopup, Action OpenLauncher);

public sealed class FeatureWindow : Component<FeatureWindowProps>
{
    public override Element Render()
    {
        var clicks = UseRef(0);
        var (shown, setShown) = UseState(0);
        var (typed, setTyped) = UseState("");
        Element[] rows = [.. Props.Services.Select(row => Component<Row, RowProps>(row).WithKey(row.Id))];
        return ScrollViewer(VStack(2,
        [
            Component<Row, RowProps>(Props.Floating).WithKey(Props.Floating.Id),
            HStack(8,
                Button("Click", () => setShown(++clicks.Current)).AutomationId("features.click"),
                TextBlock($"clicks: {shown}").AutomationId("features.clicks").VAlign(VerticalAlignment.Center),
                Button("Popup", Props.OpenPopup).AutomationId("features.popup"),
                Button("Launcher", Props.OpenLauncher).AutomationId("features.launcher")),
            HStack(8,
                TextBox(typed, setTyped, "Type here").AutomationId("features.text").AutomationName("Type here")
                    .Width(160),
                TextBlock($"typed: {typed}").AutomationId("features.typed").VAlign(VerticalAlignment.Center)),
            Elements.Surface(VStack(2,
                    TextBlock("surface: painted").AutomationId("block.surface"),
                    Elements.WidgetArea(defaults: ImmutableArray.Create(
                        new Placement("feature", new Dictionary<string, object> { ["text"] = "placed" })))),
                new Thickness(4)),
            Component<Capture>(),
            .. rows
        ]).Padding(8));
    }
}

public sealed class FeatureWidget : Widget
{
    public override Element Render()
    {
        return TextBlock($"widget area: {Setting("text", "")}").AutomationId("block.widget-area");
    }
}
