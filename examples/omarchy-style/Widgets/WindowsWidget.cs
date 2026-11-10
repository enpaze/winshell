using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace OmarchyStyle;

/// <summary>The Windows glyph at the start of the bar, where Omarchy has its logo.</summary>
public sealed class WindowsWidget : UserControl
{
    public WindowsWidget(WidgetContext context)
    {
        // As wide as a button, one slot, so it lines up with the slots after it. 32 is the built-in slot.
        Content = new TextBlock
        {
            Text = Glyphs.Windows,
            Width = context.Style?.SlotSize ?? 32,
            TextAlignment = TextAlignment.Center
        };
    }
}
