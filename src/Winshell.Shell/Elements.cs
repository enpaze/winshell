using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

/// <summary>
/// <see cref="Surface"/> and <see cref="WidgetArea"/> as elements, so a component can write them into its tree.
/// A window mounts that component with <c>new ReactorHostControl { ComponentFactory = () =&gt; new Dock() }</c>.
/// <code>
/// using static Winshell.Elements;
///
/// public sealed class Dock : Component
/// {
///     public override Element Render() =&gt; Surface(WidgetArea("dock", ["start", "tasks"]));
/// }
/// </code>
/// </summary>
public static class Elements
{
    /// <summary>A surface showing <paramref name="content"/> in <paramref name="style"/>, or the shell's style when that is null.</summary>
    public static Element Surface(Element? content = null, Thickness padding = default, ShellStyle? style = null) =>
        Component<Surface, SurfaceProps>(new SurfaceProps(content, padding, style));

    /// <summary>
    /// A widget area. <paramref name="name"/> selects its section of the preferences file, and
    /// <paramref name="defaults"/> shows until that section has a list. An empty name uses the defaults only.
    /// </summary>
    public static Element WidgetArea(string name = "", IReadOnlyList<Placement>? defaults = null,
        Orientation orientation = Orientation.Horizontal, ShellStyle? style = null) =>
        Component<WidgetArea, WidgetAreaProps>(new WidgetAreaProps(name, defaults, orientation, style));
}
