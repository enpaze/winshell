using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
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

    internal static WidgetRegistry Discover(Log log)
    {
        return Discover(ProgramAssemblyPaths(), AssemblyLoadContext.Default, log);
    }

    internal static WidgetRegistry Discover(IEnumerable<string> paths, AssemblyLoadContext context, Log log)
    {
        var registry = new WidgetRegistry();
        var shell = typeof(Widget).Assembly.GetName().Name!;
        List<Type> found = [];
        foreach (var path in paths)
            try
            {
                if (ReferencedName(path, shell) is not { } name)
                    continue;
                var assembly = context.LoadFromAssemblyName(name);
                found.AddRange(WidgetsIn(assembly));
            }
            catch (Exception e) when (Unreadable(e))
            {
                log.Warn("widgets", $"Skipped {path} while looking for widgets: {e.Message}");
            }

        ThrowIfAny(registry.Add(found));
        return registry;
    }

    internal static WidgetRegistry Of(IEnumerable<Type> types)
    {
        var registry = new WidgetRegistry();
        ThrowIfAny(registry.Add(types.Where(IsWidget)));
        return registry;
    }

    internal void Add<TWidget>(string name)
        where TWidget : Widget, new()
    {
        if (Add(name, typeof(TWidget)) is { } problem)
            throw new InvalidOperationException(problem);
    }

    internal Element Create(string name, WidgetContext context)
    {
        return _widgets.TryGetValue(name, out var widget)
            ? widget.Create(context)
            : throw new KeyNotFoundException("No widget is registered with this name.");
    }

    internal static string NameOf(string className)
    {
        var name = className.Length > Suffix.Length && className.EndsWith(Suffix, StringComparison.Ordinal)
            ? className[..^Suffix.Length]
            : className;
        return WordBoundary().Replace(name, "-").ToLowerInvariant();
    }

    private static List<Type> WidgetsIn(Assembly assembly)
    {
        return [.. assembly.GetExportedTypes().Where(IsWidget)];
    }

    private List<string> Add(IEnumerable<Type> types)
    {
        List<string> problems = [];
        foreach (var type in types.OrderBy(type => type.FullName, StringComparer.Ordinal))
            if (Add(NameOf(type), type) is { } problem)
                problems.Add(problem);
        return problems;
    }

    private string? Add(string? name, Type type)
    {
        if (string.IsNullOrWhiteSpace(name))
            return $"{type.FullName} has an empty widget name. Give [WidgetName] a name.";
        if (type.GetConstructor(Type.EmptyTypes) is null)
            return $"{type.FullName} is a widget with no public parameterless constructor. Add one, or make the class abstract.";
        if (_widgets.TryGetValue(name, out var other))
            return $"Two widgets are named {name}: {other.Type.FullName} and {type.FullName}. Rename one class, or give it a [WidgetName].";
        _widgets.Add(name, (type, Factory(type)));
        return null;
    }

    private static void ThrowIfAny(List<string> problems)
    {
        if (problems.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, problems));
    }

    private static string? NameOf(Type type)
    {
        return type.GetCustomAttribute<WidgetNameAttribute>() is { } attribute ? attribute.Name : NameOf(type.Name);
    }

    private static bool IsWidget(Type type)
    {
        return type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } &&
               type.IsSubclassOf(typeof(Widget));
    }

    private static bool Unreadable(Exception e)
    {
        return e is IOException or UnauthorizedAccessException or BadImageFormatException or TypeLoadException
            or ReflectionTypeLoadException;
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

    internal static IEnumerable<string> ProgramAssemblyPaths()
    {
        var paths = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var runtime = Path.TrimEndingDirectorySeparator(RuntimeEnvironment.GetRuntimeDirectory());
        var app = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        if (string.Equals(runtime, app, StringComparison.OrdinalIgnoreCase) ||
            Path.GetDirectoryName(Path.GetDirectoryName(runtime)) is not { } shared)
            return paths;
        shared += Path.DirectorySeparatorChar;
        return paths.Where(path => !path.StartsWith(shared, StringComparison.OrdinalIgnoreCase));
    }

    private static AssemblyName? ReferencedName(string path, string shell)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata || pe.GetMetadataReader() is not { IsAssembly: true } reader)
            throw new BadImageFormatException("It is not a .NET assembly.");
        foreach (var handle in reader.AssemblyReferences)
            if (reader.StringComparer.Equals(reader.GetAssemblyReference(handle).Name, shell))
                return reader.GetAssemblyDefinition().GetAssemblyName();
        return null;
    }

    [GeneratedRegex(@"(?<=\p{Ll})(?=\p{Lu})|(?<=\p{Lu})(?=\p{Lu}\p{Ll})|(?<=\p{L})(?=\p{Nd})|(?<=\p{Nd})(?=\p{L})")]
    private static partial Regex WordBoundary();
}
