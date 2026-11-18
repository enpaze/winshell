using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

internal sealed partial class WidgetRegistry
{
    private const string Suffix = "Widget";

    private readonly Dictionary<string, (Type Type, Func<WidgetContext, Element> Create)> _widgets =
        new(StringComparer.Ordinal);

    internal IEnumerable<string> Names => _widgets.Keys;

    internal static WidgetRegistry Discover()
    {
        return Of(ProgramAssemblies().SelectMany(assembly => assembly.GetExportedTypes()));
    }

    internal static WidgetRegistry Of(IEnumerable<Type> types)
    {
        var registry = new WidgetRegistry();
        foreach (var type in types.Where(IsWidget).OrderBy(type => type.FullName, StringComparer.Ordinal))
            registry.Add(NameOf(type), type);
        return registry;
    }

    internal void Add<TWidget>(string name)
        where TWidget : Widget, new()
    {
        Add(name, typeof(TWidget));
    }

    internal Element Create(string name, WidgetContext context)
    {
        return _widgets.TryGetValue(name, out var widget)
            ? widget.Create(context)
            : throw new KeyNotFoundException("No widget is registered with this name.");
    }

    internal static string NameOf(Type type)
    {
        return type.GetCustomAttribute<WidgetNameAttribute>()?.Name ?? NameOf(type.Name);
    }

    internal static string NameOf(string className)
    {
        var name = className.Length > Suffix.Length && className.EndsWith(Suffix, StringComparison.Ordinal)
            ? className[..^Suffix.Length]
            : className;
        return WordBoundary().Replace(name, "-").ToLowerInvariant();
    }

    private void Add(string name, Type type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException($"{type.FullName} has an empty widget name. Give [WidgetName] a name.");
        if (type.GetConstructor(Type.EmptyTypes) is null)
            throw new InvalidOperationException(
                $"{type.FullName} is a widget with no public parameterless constructor. Add one, or make the class abstract.");
        if (_widgets.TryGetValue(name, out var other))
            throw new InvalidOperationException(
                $"Two widgets are named {name}: {other.Type.FullName} and {type.FullName}. Rename one class, or give it a [WidgetName].");
        _widgets.Add(name, (type, Factory(type)));
    }

    private static bool IsWidget(Type type)
    {
        return type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } &&
               type.IsSubclassOf(typeof(Widget));
    }

    private static Func<WidgetContext, Element> Factory(Type type)
    {
        return typeof(WidgetRegistry).GetMethod(nameof(ElementOf), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type)
            .CreateDelegate<Func<WidgetContext, Element>>();
    }

    private static Element ElementOf<TWidget>(WidgetContext context)
        where TWidget : Widget, new()
    {
        return Component<TWidget, WidgetContext>(context);
    }

    private static IEnumerable<Assembly> ProgramAssemblies()
    {
        var shell = typeof(Widget).Assembly.GetName().Name!;
        var paths = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in paths)
            if (ReferencedName(path, shell) is { } name)
                yield return AssemblyLoadContext.Default.LoadFromAssemblyName(name);
    }

    private static AssemblyName? ReferencedName(string path, string shell)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata)
            return null;
        var reader = pe.GetMetadataReader();
        if (!reader.IsAssembly)
            return null;
        foreach (var handle in reader.AssemblyReferences)
            if (reader.StringComparer.Equals(reader.GetAssemblyReference(handle).Name, shell))
                return reader.GetAssemblyDefinition().GetAssemblyName();
        return null;
    }

    [GeneratedRegex(@"(?<=\p{Ll})(?=\p{Lu})|(?<=\p{Lu})(?=\p{Lu}\p{Ll})|(?<=\p{L})(?=\p{Nd})|(?<=\p{Nd})(?=\p{L})")]
    private static partial Regex WordBoundary();
}
