using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

/// <summary>
/// What a <see cref="Surface"/> shows. <see cref="Content"/> is one element, often a <see cref="WidgetArea"/>, and it
/// stays loaded when the style changes. A null <see cref="Style"/> follows <see cref="Shell.Style"/>.
/// </summary>
/// <param name="Content">What the surface holds, or null for nothing.</param>
/// <param name="Padding">The space around <paramref name="Content"/>, in logical pixels.</param>
/// <param name="Style">The surface's own style, which wins over the shell's, or null for the shell's.</param>
public sealed record SurfaceProps(Element? Content = null, Thickness Padding = default, ShellStyle? Style = null);

/// <summary>
/// Paints the style's <c>ShellBackgroundBrush</c> behind its content in the window's theme, as plain controls take
/// theirs, so a switch between light and dark while the shell runs reaches it. A bar's window shows one of these as
/// its tree, with the bar's areas inside it.
/// </summary>
public sealed class Surface : Component<SurfaceProps>
{
    /// <summary>The background under the content. The content is not keyed, so a new style leaves it loaded.</summary>
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

        // The brush is the one the style dictionary already loaded. A theme switch reads it again.
        void Paint(FrameworkElement element)
        {
            StyleHost.Show(element, shown.Current);
            if (layer.Current is { } border && element.Resources["ShellBackgroundBrush"] is Brush brush)
                border.Background = brush;
        }
    }
}
