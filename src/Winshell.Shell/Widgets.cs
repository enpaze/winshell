using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;

namespace Winshell;

/// <summary>The widget components available to this configuration, registered before creating its areas.</summary>
public static class Widgets
{
    private static readonly Dictionary<string, Func<WidgetContext, string, string, UIElement>> Registered = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

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
        Add(name, (context, widget, area) => new ComponentWidgetHost<TWidget>(context, widget, area));
    }

    private static void Add(string name, Func<WidgetContext, string, string, UIElement> create)
    {
        lock (Gate)
            Registered.Add(name, create);
    }

    internal static UIElement Create(string name, WidgetContext context, string area)
    {
        Func<WidgetContext, string, string, UIElement> create;
        lock (Gate)
            create = Registered.TryGetValue(name, out var found)
                ? found
                : throw new KeyNotFoundException("No widget is registered with this name.");
        return create(context, name, area);
    }
}
