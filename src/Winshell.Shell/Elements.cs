using System.Collections.Immutable;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

public static class Elements
{
    public static Element Surface(Element? content = null, Thickness padding = default, ShellStyle? style = null) =>
        Component<Surface, SurfaceProps>(new SurfaceProps(content, padding, style));

    public static Element WidgetArea(string name = "", ImmutableArray<Placement>? defaults = null,
        Orientation orientation = Orientation.Horizontal, ShellStyle? style = null) =>
        Component<WidgetArea, WidgetAreaProps>(new WidgetAreaProps(name, defaults, orientation, style));

    public static Element Alert(this Element element, bool on = true) =>
        Component<AlertHost, AlertProps>(new AlertProps(element, on));
}
