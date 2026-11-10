using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;

namespace Winshell;

/// <summary>The widget factories available to this configuration, registered before creating its areas.</summary>
public static class Widgets
{
    private static readonly Dictionary<string, Factory> Registered = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    /// <summary>
    /// Registers a case-sensitive name for the life of the program. A duplicate name throws. The factory runs on the
    /// shell's thread and must return a fresh element for each placement. Only failures during creation are contained.
    /// </summary>
    public static void Register(string name, Func<WidgetContext, UIElement> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(create);
        Add(name, new ControlFactory(create));
    }

    /// <summary>
    /// Registers a component widget under a case-sensitive name for the life of the program. A duplicate name throws.
    /// One instance is made for each placement, on the shell's thread, and a changed placement renders that instance
    /// again with the new <see cref="WidgetContext"/>. A component that throws while rendering shows its error in the
    /// area, and the area logs it.
    /// </summary>
    /// <typeparam name="TWidget">The component. Its props are the placement's <see cref="WidgetContext"/>.</typeparam>
    public static void Register<TWidget>(string name)
        where TWidget : Component<WidgetContext>, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Add(name, new ComponentFactory<TWidget>());
    }

    private static void Add(string name, Factory factory)
    {
        lock (Gate)
            Registered.Add(name, factory);
    }

    internal static UIElement Create(string name, WidgetContext context, string area)
    {
        Factory factory;
        lock (Gate)
            factory = Registered.TryGetValue(name, out var found)
                ? found
                : throw new KeyNotFoundException("No widget is registered with this name.");
        return factory.Create(context, name, area);
    }

    private abstract class Factory
    {
        public abstract UIElement Create(WidgetContext context, string name, string area);
    }

    private sealed class ControlFactory(Func<WidgetContext, UIElement> create) : Factory
    {
        public override UIElement Create(WidgetContext context, string name, string area) =>
            create(context) ?? throw new InvalidOperationException("The widget factory returned null.");
    }

    private sealed class ComponentFactory<TWidget> : Factory
        where TWidget : Component<WidgetContext>, new()
    {
        public override UIElement Create(WidgetContext context, string name, string area) =>
            new ComponentWidgetHost<TWidget>(context, name, area);
    }
}
