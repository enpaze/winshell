using Microsoft.UI.Xaml;

namespace OmarchyStyle;

public sealed class WindowsWidget : Widget
{
    public override Element Render()
    {
        return TextBlock(Glyphs.Windows)
            .Width(Style.SlotSize)
            .AutomationId("windows")
            .Set(text => text.TextAlignment = TextAlignment.Center);
    }
}
