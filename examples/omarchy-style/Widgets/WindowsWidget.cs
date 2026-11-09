using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace OmarchyStyle;

/// <summary>The Windows glyph at the start of the bar, where Omarchy has its logo.</summary>
public sealed class WindowsWidget : UserControl
{
    public WindowsWidget(WidgetContext context)
    {
        // As wide as a button, so it lines up with the slots after it.
        Content = new TextBlock { Text = Glyphs.Windows, Width = 27, TextAlignment = TextAlignment.Center };
    }
}
