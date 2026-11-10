using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Winshell;
using static Microsoft.UI.Reactor.Factories;

namespace OmarchyStyle;

/// <summary>The Windows glyph at the start of the bar, where Omarchy has its logo.</summary>
public sealed class WindowsWidget : Component<WidgetContext>
{
    public override Element Render()
    {
        // As wide as a button, one slot, so it lines up with the slots after it. 32 is the built-in slot.
        return TextBlock(Glyphs.Windows)
            .Width(Props.Style?.SlotSize ?? 32)
            .Set(text => text.TextAlignment = TextAlignment.Center);
    }
}
