using Microsoft.UI.Xaml;

namespace Winshell;

/// <summary>The widget factories available to this configuration, registered before creating its areas.</summary>
public static class Widgets
{
    private static readonly Dictionary<string, Func<WidgetContext, UIElement>> Factories = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    /// <summary>
    /// Registers a case-sensitive name for the life of the program. A duplicate name throws. The factory runs on the
    /// shell's thread and must return a fresh element for each placement. Only failures during creation are contained.
    /// </summary>
    public static void Register(string name, Func<WidgetContext, UIElement> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(create);
        lock (Gate)
            Factories.Add(name, create);
    }

    internal static UIElement Create(string name, WidgetContext context)
    {
        Func<WidgetContext, UIElement> create;
        lock (Gate)
            create = Factories.TryGetValue(name, out var factory)
                ? factory
                : throw new KeyNotFoundException("No widget is registered with this name.");
        return create(context) ?? throw new InvalidOperationException("The widget factory returned null.");
    }
}
