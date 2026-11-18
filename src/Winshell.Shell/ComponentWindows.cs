using System.Runtime.CompilerServices;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

public sealed class PanelWindow<T> : PanelWindow where T : Component, new()
{
    public PanelWindow()
    {
        Content = ComponentHost.For<T>(this);
    }
}

public sealed class PopupWindow<T> : PopupWindow where T : Component, new()
{
    public PopupWindow()
    {
        Content = ComponentHost.For<T>(this);
    }
}

public sealed class FloatingWindow<T> : FloatingWindow where T : Component, new()
{
    public FloatingWindow()
    {
        Content = ComponentHost.For<T>(this);
    }
}

internal static class ComponentHost
{
    private static readonly ConditionalWeakTable<UIElement, Type> Shown = [];

    public static ReactorHostControl For<T>(ShellWindow window) where T : Component, new()
    {
        var host = new ReactorHostControl
        {
            ComponentFactory = () => new Surface(),
            Props = new SurfaceProps(Component<T>()),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            IsTabStop = false
        };
        Shown.Add(host, typeof(T));
        window.Disposing += host.Dispose;
        return host;
    }

    public static Type? ShownBy(UIElement content)
    {
        return Shown.TryGetValue(content, out var type) ? type : null;
    }
}
