using Microsoft.UI.Xaml;

namespace OmarchyStyle;

public sealed class WindowsWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        return TextBlock(Glyphs.Windows)
            .Width(Props.Style?.SlotSize ?? 32)
            .AutomationId("windows")
            .Set(text => text.TextAlignment = TextAlignment.Center);
    }
}
