using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

public static class Widgets
{
    private static readonly Dictionary<string, Func<WidgetContext, Element>> Registered = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    public static void Register<TWidget>(string name)
        where TWidget : Component<WidgetContext>, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Add(name, context => Component<TWidget, WidgetContext>(context));
    }

    private static void Add(string name, Func<WidgetContext, Element> create)
    {
        lock (Gate)
            Registered.Add(name, create);
    }

    internal static Element Create(string name, WidgetContext context)
    {
        Func<WidgetContext, Element> create;
        lock (Gate)
            create = Registered.TryGetValue(name, out var found)
                ? found
                : throw new KeyNotFoundException("No widget is registered with this name.");
        return create(context);
    }
}
