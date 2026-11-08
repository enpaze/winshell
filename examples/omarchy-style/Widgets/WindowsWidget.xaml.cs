using Microsoft.UI.Xaml;
using Winshell;

namespace OmarchyStyle;

/// <summary>The Windows glyph at the start of the bar.</summary>
public sealed partial class WindowsWidget
{
    public WindowsWidget(WidgetContext context)
    {
        InitializeComponent();
        Glyph.Text = Glyphs.Windows;
        _ = context;
        Loaded += (_, _) =>
        {
            Glyph.Style = Bar.Resource<Style>(this, "PanelText");
            Glyph.Width = Bar.Resource<double>(this, "SlotWidth");
        };
    }
}
