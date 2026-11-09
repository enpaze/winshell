using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winshell;

namespace OmarchyStyle;

/// <summary>
/// The panel on one screen. Three widget areas hold the bar. The default layout names them <c>start</c>,
/// <c>center</c> and <c>end</c>; a layout called <c>side</c> names them <c>side.start</c>, <c>side.center</c> and
/// <c>side.end</c>. Left and right edges run the areas vertically. With no lists in the preferences file the areas
/// use the defaults.
/// </summary>
public sealed partial class PanelView : IDisposable
{
    /// <summary>The height of a top or bottom bar, which that bar also reserves.</summary>
    public double Size => (double)Resources["PanelHeight"];

    private string _edge = "top";
    private string _layout = "";
    private WidgetArea[] _areas = [];

    public PanelView()
    {
        InitializeComponent();
    }

    /// <summary>Lays this bar along <paramref name="edge"/>, using <paramref name="layout"/>'s area names.</summary>
    public void Apply(string edge, string layout)
    {
        _edge = edge;
        _layout = layout;
        var vertical = BarLayout.Vertical(edge);
        var inset = (Thickness)Resources["PanelInset"];
        Root.Padding = vertical ? new Thickness(inset.Top, inset.Left, inset.Bottom, inset.Right) : inset;
        if (vertical)
        {
            StartArea.HorizontalAlignment = HorizontalAlignment.Stretch;
            StartArea.VerticalAlignment = VerticalAlignment.Top;
            CenterArea.HorizontalAlignment = HorizontalAlignment.Stretch;
            CenterArea.VerticalAlignment = VerticalAlignment.Center;
            EndArea.HorizontalAlignment = HorizontalAlignment.Stretch;
            EndArea.VerticalAlignment = VerticalAlignment.Bottom;
        }
        else
        {
            StartArea.HorizontalAlignment = HorizontalAlignment.Left;
            StartArea.VerticalAlignment = VerticalAlignment.Stretch;
            CenterArea.HorizontalAlignment = HorizontalAlignment.Center;
            CenterArea.VerticalAlignment = VerticalAlignment.Center;
            EndArea.HorizontalAlignment = HorizontalAlignment.Right;
            EndArea.VerticalAlignment = VerticalAlignment.Stretch;
        }

        var orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        if (_areas.Length > 0 && !ReferenceEquals(_areas[0], StartArea))
        {
            foreach (var area in _areas)
                area.Dispose();
        }

        Fill(StartArea, orientation, BarLayout.Area(layout, "start"), new Placement("windows"), new Placement("workspaces"));
        Fill(CenterArea, orientation, BarLayout.Area(layout, "center"), new Placement("clock"));
        Fill(EndArea, orientation, BarLayout.Area(layout, "end"),
            new Placement("tray"), new Placement("bluetooth"), new Placement("network"), new Placement("audio"),
            new Placement("screen"), new Placement("battery"));
        _areas = [StartArea, CenterArea, EndArea];
    }

    private void OnLayoutLoaded(object sender, RoutedEventArgs e)
    {
        // Live XAML editing replaces the areas. The first load uses the ones Apply placed.
        if (_areas.Length == 3 && ReferenceEquals(_areas[0], StartArea) && ReferenceEquals(_areas[1], CenterArea) &&
            ReferenceEquals(_areas[2], EndArea))
            return;
        Apply(_edge, _layout);
    }

    private void Fill(WidgetArea area, Orientation orientation, string name, params Placement[] placements)
    {
        // Orientation first, so the widgets are created for the direction they will run in.
        area.Orientation = orientation;
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
