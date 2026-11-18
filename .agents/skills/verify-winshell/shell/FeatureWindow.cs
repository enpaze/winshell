using System.Collections.Immutable;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace FeatureMap;

public sealed record FeatureWindowProps(RowProps[] Services, RowProps Floating, Action OpenPopup);

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
                Button("Popup", Props.OpenPopup).AutomationId("features.popup")),
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

public sealed class FeatureWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        return TextBlock($"widget area: {Props.Settings.GetValueOrDefault("text")}").AutomationId("block.widget-area");
    }
}
