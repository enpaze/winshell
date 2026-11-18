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
        var scopeRef = UseRef<StyleScope?>(null);
        var scope = scopeRef.Current ??= new StyleScope { Style = () => shown.Current ?? Shell.Current.Style };
        var (_, bump) = UseReducer(0);
        UseEffect(StyleHost.FollowShellStyle(props.Style, () => bump(_ => _ + 1)), props.Style);

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
            .Provide(StyleHost.InEffect, scope)
            .OnMount(element =>
            {
                root.Current = element;
                Paint(element);
                element.ActualThemeChanged += (_, _) => Paint(element);
            });

        void Paint(FrameworkElement element)
        {
            if (StyleHost.Show(element, shown.Current))
                scope.RaiseStyleChanged();
            if (layer.Current is { } border && element.Resources["ShellBackgroundBrush"] is Brush brush)
                border.Background = brush;
        }
    }
}
