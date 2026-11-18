using System.Collections.Immutable;
using Microsoft.UI.Xaml;

namespace FeatureMap;

public sealed record FeatureWindowProps(RowProps[] Services, RowProps Floating, Action OpenPopup, Action OpenLauncher);

public sealed class FeatureWindow : Component
{
    public override Element Render()
    {
        var props = Features.Main;
        var clicks = UseRef(0);
        var (shown, setShown) = UseState(0);
        var (typed, setTyped) = UseState("");
        Element[] rows = [.. props.Services.Select(row => Component<Row, RowProps>(row).WithKey(row.Id))];
        return ScrollViewer(VStack(2,
        [
            Component<Row, RowProps>(props.Floating).WithKey(props.Floating.Id),
            HStack(8,
                Button("Click", () => setShown(++clicks.Current)).AutomationId("features.click"),
                TextBlock($"clicks: {shown}").AutomationId("features.clicks").VAlign(VerticalAlignment.Center),
                Button("Popup", props.OpenPopup).AutomationId("features.popup"),
                Button("Launcher", props.OpenLauncher).AutomationId("features.launcher")),
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
