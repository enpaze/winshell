using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Winshell;

namespace OmarchyStyle;

/// <summary>
/// The panel on one screen. Three widget areas hold the bar: the Windows glyph and workspaces at the start, the clock
/// in the center, and the tray and status glyphs at the end. With no placement file those are the defaults.
/// </summary>
public sealed partial class PanelView : IDisposable
{
    /// <summary>The panel's height, which it also reserves.</summary>
    public double Size => (double)Resources["PanelHeight"];

    private WidgetArea[] _areas = [];

    public PanelView()
    {
        InitializeComponent();
        // Widgets are created here, before the panel is in a window. A screen-aware widget reads its screen once loaded.
        Place();
    }

    private void OnLayoutLoaded(object sender, RoutedEventArgs e)
    {
        // Live XAML editing replaces the areas. The first load uses the ones the constructor placed.
        if (_areas.Length == 3 && ReferenceEquals(_areas[0], StartArea) && ReferenceEquals(_areas[1], CenterArea) &&
            ReferenceEquals(_areas[2], EndArea))
            return;
        Place();
    }

    private void Place()
    {
        foreach (var area in _areas)
            area.Dispose();
        Fill(StartArea, "start", new Placement("windows"), new Placement("workspaces"));
        Fill(CenterArea, "center", new Placement("clock"));
        Fill(EndArea, "end",
            new Placement("tray"), new Placement("bluetooth"), new Placement("network"), new Placement("audio"),
            new Placement("screen"), new Placement("battery"));
        _areas = [StartArea, CenterArea, EndArea];
    }

    private void Fill(WidgetArea area, string name, params Placement[] placements)
    {
        area.Name = name;
        area.FontFamily = (FontFamily)Resources["PanelFont"];
        area.FontSize = (double)Resources["PanelFontSize"];
        area.Foreground = (Brush)Resources["PanelForeground"];
        area.Defaults = placements;
    }

    public void Dispose()
    {
        foreach (var area in _areas)
            area.Dispose();
        _areas = [];
    }
}
