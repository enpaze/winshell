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
        Content = ComponentHost.For(this, typeof(T), Component<T>());
    }
}

public sealed class PanelWindow<T, TProps> : PanelWindow where T : Component<TProps>, new()
{
    public PanelWindow(TProps props)
    {
        Content = ComponentHost.For(this, typeof(T), Component<T, TProps>(props));
    }
}

public sealed class PopupWindow<T> : PopupWindow where T : Component, new()
{
    public PopupWindow()
    {
        Content = ComponentHost.For(this, typeof(T), Component<T>());
    }
}

public sealed class PopupWindow<T, TProps> : PopupWindow where T : Component<TProps>, new()
{
    public PopupWindow(TProps props)
    {
        Content = ComponentHost.For(this, typeof(T), Component<T, TProps>(props));
    }
}

public sealed class FloatingWindow<T> : FloatingWindow where T : Component, new()
{
    public FloatingWindow()
    {
        Content = ComponentHost.For(this, typeof(T), Component<T>());
    }
}

public sealed class FloatingWindow<T, TProps> : FloatingWindow where T : Component<TProps>, new()
{
    public FloatingWindow(TProps props)
    {
        Content = ComponentHost.For(this, typeof(T), Component<T, TProps>(props));
    }
}

internal static class ComponentHost
{
    private static readonly ConditionalWeakTable<UIElement, Type> Shown = [];

    public static ReactorHostControl For(ShellWindow window, Type shown, Element component)
    {
        var host = new ReactorHostControl
        {
            ComponentFactory = () => new Surface(),
            Props = new SurfaceProps(component),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            IsTabStop = false
        };
        Shown.Add(host, shown);
        window.Disposing += host.Dispose;
        return host;
    }

    public static Type? ShownBy(UIElement content)
    {
        return Shown.TryGetValue(content, out var type) ? type : null;
    }
}
