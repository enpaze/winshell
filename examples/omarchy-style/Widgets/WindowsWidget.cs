using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

public sealed class WindowsWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        return TextBlock(Glyphs.Windows)
            .Width(Props.Style?.SlotSize ?? 32)
            .Set(text => text.TextAlignment = TextAlignment.Center);
    }
}
