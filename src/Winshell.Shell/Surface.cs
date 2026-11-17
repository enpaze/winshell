using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

public sealed record SurfaceProps(Element? Content = null, Thickness Padding = default, ShellStyle? Style = null);

public sealed class Surface : Component<SurfaceProps>
{
    public override Element Render()
    {
        var props = Props;
        var root = UseRef<FrameworkElement?>(null);
        var layer = UseRef<Border?>(null);
        var shown = UseRef(props.Style);
        shown.Current = props.Style;
        var (_, bump) = UseReducer(0);
        UseEffect(() =>
        {
            if (props.Style is not null)
                return () => { };
            void Follow() => bump(_ => _ + 1);
            var shell = Shell.Current;
            shell.StyleChanged += Follow;
            return () => shell.StyleChanged -= Follow;
        }, props.Style);

        if (root.Current is { } mounted)
            Paint(mounted);

        var content = (props.Content ?? Empty()).Padding(props.Padding);
        return Grid(
                [GridSize.Star()],
                [GridSize.Star()],
                XamlHost(() =>
                {
                    var border = new Border
                    {
                        IsHitTestVisible = false,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };
                    layer.Current = border;
                    return border;
                }).Grid(0, 0),
                content.Grid(0, 0))
            .OnMount(element =>
            {
                root.Current = element;
                Paint(element);
                element.ActualThemeChanged += (_, _) => Paint(element);
            });

        void Paint(FrameworkElement element)
        {
            StyleHost.Show(element, shown.Current);
            if (layer.Current is { } border && element.Resources["ShellBackgroundBrush"] is Brush brush)
                border.Background = brush;
        }
    }
}
