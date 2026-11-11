using System.Globalization;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
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
        var (ready, setReady) = UseState(false);
        var root = UseRef<FrameworkElement?>(null);
        var target = UseRef<FrameworkElement?>(null);
        var seen = UseRef(false);
        var last = UseRef<ShellStyle?>(null);
        var stamp = UseRef(0);
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

        var resolved = props.Style ?? Shell.Current.Style;
        if (!seen.Current || !Equals(last.Current, resolved))
        {
            if (seen.Current)
                stamp.Current++;
            seen.Current = true;
            last.Current = resolved;
        }

        if (root.Current is { } mounted)
            Restyle(mounted);

        // The brush is a {ThemeResource} read where this XAML is parsed, so the style dictionary has to be in that
        // dictionary. A new style parses it again. The content is not keyed, so it stays loaded. It is held back
        // until the dictionary is on the host, so what it holds inherits the style on its first render.
        var content = ready ? (props.Content ?? Empty()).Padding(props.Padding) : Empty();
        return Grid(
                [GridSize.Star()],
                [GridSize.Star()],
                XamlHost(() => BackgroundLayer(resolved))
                    .WithKey(string.Create(CultureInfo.InvariantCulture, $"background{stamp.Current}"))
                    .Grid(0, 0),
                content.Grid(0, 0))
            .OnMount(element =>
            {
                if (!ReferenceEquals(root.Current, element))
                    target.Current = null;
                root.Current = element;
                Restyle(element);
                setReady(true);
                element.Loaded += (_, _) => bump(_ => _ + 1);
            });

        void Restyle(FrameworkElement element)
        {
            var current = target.Current;
            StyleHost.Place(element, props.Style, ref current);
            target.Current = current;
        }
    }

    /// <summary>
    /// A layer whose template paints <c>ShellBackgroundBrush</c>. Parsed with the style merged in, so the theme
    /// resource resolves, and a theme switch reaches it without a new layer.
    /// </summary>
    private static ContentControl BackgroundLayer(ShellStyle? style)
    {
        var xaml = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                   "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
                   "<ResourceDictionary.MergedDictionaries>" + Shell.DictionaryXaml(style) +
                   "</ResourceDictionary.MergedDictionaries>" +
                   "<ControlTemplate x:Key=\"SurfaceBackground\" TargetType=\"ContentControl\">" +
                   "<Border Background=\"{ThemeResource ShellBackgroundBrush}\" />" +
                   "</ControlTemplate></ResourceDictionary>";
        var dictionary = (ResourceDictionary)XamlReader.Load(xaml);
        return new ContentControl
        {
            IsTabStop = false,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Template = (ControlTemplate)dictionary["SurfaceBackground"]
        };
    }
}
