using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Winshell.Docs;

internal static class ReferencePages
{
    private const string Marker = "<!-- generated below: dotnet run scripts/generate-reference.cs -->";

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static SortedDictionary<string, string> Generate(string folder)
    {
        Assembly[] assemblies = [typeof(Shell).Assembly, typeof(ShellIpc).Assembly, typeof(Log).Assembly];
        var included = Included(assemblies);
        var pages = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in included.GroupBy(type => type.Namespace!))
        {
            var file = FileName(group.Key);
            var page = new Page(group.Key, included);
            var text = new StringBuilder(Top(Path.Combine(folder, file), group.Key));
            foreach (var type in group.OrderBy(type => type.Name, StringComparer.Ordinal))
                text.Append(page.Section(type));
            pages[file] = text.ToString().TrimEnd('\n') + "\n";
        }

        return pages;
    }

    public static List<string> Stale(string folder, SortedDictionary<string, string> pages)
    {
        var stale = pages
            .Where(page => !File.Exists(Path.Combine(folder, page.Key)) ||
                           File.ReadAllText(Path.Combine(folder, page.Key)).ReplaceLineEndings("\n") != page.Value)
            .Select(page => page.Key)
            .ToList();
        stale.AddRange(Directory.EnumerateFiles(folder, "winshell*.md").Select(path => Path.GetFileName(path))
            .Where(file => !pages.ContainsKey(file)));
        return stale;
    }

    private static string FileName(string ns)
    {
        return ns == "Winshell" ? "winshell.md" : ns.ToLowerInvariant().Replace('.', '-') + ".md";
    }

    private static string Top(string path, string ns)
    {
        if (!File.Exists(path))
            return $"# {ns}\n\n{Marker}\n";
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        var marker = text.IndexOf(Marker + "\n", StringComparison.Ordinal);
        if (marker < 0)
            throw new InvalidOperationException($"{path} has no line {Marker}");
        return text[..(marker + Marker.Length + 1)];
    }

    private sealed class Page(string ns, HashSet<Type> included)
    {
        private readonly NullabilityInfoContext _nullability = new();

        public string Section(Type type)
        {
            var text = new StringBuilder($"\n## {Title(type)}\n\n");
            if (type.IsEnum)
            {
                text.Append("| Value | Number |\n| --- | --- |\n");
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    var number = Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture);
                    text.Append(CultureInfo.InvariantCulture, $"| `{field.Name}` | {number} |\n");
                }

                return text.ToString();
            }

            if (Inherits(type) is { } line)
                text.Append(line).Append("\n\n");
            var rows = Rows(type).ToList();
            if (rows.Count == 0)
                return text.ToString();
            text.Append("| Member | Type |\n| --- | --- |\n");
            foreach (var (cell, kind) in rows)
                text.Append(CultureInfo.InvariantCulture, $"| `{cell}` | {kind} |\n");

            return text.ToString();
        }

        private string? Inherits(Type type)
        {
            if (type.IsInterface || type.IsValueType || type.BaseType is not { } baseType || baseType == typeof(object))
                return null;
            if (included.Contains(baseType))
                return $"Has the members of {Link(baseType)}.";
            if (baseType == typeof(Exception))
                return "An exception.";
            if (baseType == typeof(Attribute))
                return "An attribute.";
            var winui = baseType.Namespace?.StartsWith("Microsoft.UI", StringComparison.Ordinal) == true
                ? "WinUI "
                : "";
            return $"A {winui}{Bare(baseType.Name)}. The members here are the ones Winshell adds.";
        }

        private IEnumerable<(string Cell, string Kind)> Rows(Type type)
        {
            foreach (var ctor in type.GetConstructors(Declared).OrderBy(ctor => ctor.GetParameters().Length))
                yield return ($"{CodeName(type)}({Parameters(ctor)})", "constructor");
            foreach (var property in type.GetProperties(Declared | BindingFlags.NonPublic)
                         .Where(member => !Skip(member) && Shown(member.GetMethod ?? member.SetMethod!))
                         .OrderBy(member => member.Name, StringComparer.Ordinal))
            {
                var accessor = property.GetMethod ?? property.SetMethod!;
                var kind = Name(property.PropertyType, _nullability.Create(property).ReadState) +
                           (accessor.IsStatic ? ", static" : "") + (accessor.IsPublic ? "" : ", protected");
                if (property.SetMethod is not { IsPublic: true } setter)
                    kind += ", read-only";
                else if (setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
                    kind += ", init";
                yield return (property.Name, kind);
            }

            foreach (var field in type.GetFields(Declared).Where(member => !Skip(member))
                         .OrderBy(member => member.Name, StringComparer.Ordinal))
            {
                var kind = Name(field.FieldType, _nullability.Create(field).ReadState) +
                           (field.IsStatic ? ", static" : "");
                yield return (field.Name, kind);
            }

            foreach (var evt in type.GetEvents(Declared).Where(member => !Skip(member))
                         .OrderBy(member => member.Name, StringComparer.Ordinal))
                yield return (evt.Name, Name(evt.EventHandlerType!));
            var methods = type.GetMethods(Declared | BindingFlags.NonPublic)
                .Where(method => !method.IsSpecialName && !Skip(method) && Shown(method))
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ThenBy(method => method.GetParameters().Length);
            foreach (var method in methods)
            {
                var generic = method.IsGenericMethod
                    ? "<" + string.Join(", ", method.GetGenericArguments().Select(t => t.Name)) + ">"
                    : "";
                var kind = Name(method.ReturnType, _nullability.Create(method.ReturnParameter).ReadState) +
                           (method.IsStatic ? ", static" : "") + (method.IsPublic ? "" : ", protected");
                yield return ($"{method.Name}{generic}({Parameters(method)})", kind);
            }
        }

        private string Parameters(MethodBase method)
        {
            return string.Join(", ", method.GetParameters().Select(parameter =>
            {
                var modifier = parameter switch
                {
                    { IsOut: true } => "out ",
                    { IsIn: true } => "in ",
                    { ParameterType.IsByRef: true } => "ref ",
                    _ => ""
                };
                var text =
                    $"{modifier}{Name(parameter.ParameterType, _nullability.Create(parameter).ReadState)} {parameter.Name}";
                return parameter.HasDefaultValue ? $"{text} = {Literal(parameter)}" : text;
            }));
        }

        private static string Literal(ParameterInfo parameter)
        {
            return parameter.DefaultValue switch
            {
                null when parameter.ParameterType.IsValueType &&
                          Nullable.GetUnderlyingType(parameter.ParameterType) is null => "default",
                null => "null",
                string text => $"\"{text}\"",
                bool value => value ? "true" : "false",
                Enum value => $"{value.GetType().Name}.{value}",
                var value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
            };
        }

        private string Name(Type type, NullabilityState state = NullabilityState.Unknown)
        {
            var name = type switch
            {
                _ when Nullable.GetUnderlyingType(type) is { } inner => Name(inner) + "?",
                { IsByRef: true } when type.GetElementType() is { } element => Name(element),
                { IsArray: true } when type.GetElementType() is { } element => Name(element) + "[]",
                { IsGenericParameter: true } => type.Name,
                { IsGenericType: true } when
                    type.FullName?.StartsWith("System.ValueTuple`", StringComparison.Ordinal) == true =>
                    "(" + string.Join(", ", type.GetGenericArguments().Select(t => Name(t))) + ")",
                { IsGenericType: true } => Bare(type.Name) + " of " +
                                           string.Join(", ", type.GetGenericArguments().Select(t => Name(t))),
                _ when Aliases.TryGetValue(type, out var alias) => alias,
                _ when included.Contains(type) => Link(type),
                _ => type.Name
            };
            return state == NullabilityState.Nullable && !type.IsValueType ? name + "?" : name;
        }

        private string Link(Type type)
        {
            return type.Namespace == ns || type.Namespace is null
                ? $"[{type.Name}](#{type.Name.ToLowerInvariant()})"
                : $"[{type.Name}]({FileName(type.Namespace)}#{type.Name.ToLowerInvariant()})";
        }

    }

    private static readonly Dictionary<Type, string> Aliases = new()
    {
        [typeof(void)] = "void",
        [typeof(object)] = "object",
        [typeof(string)] = "string",
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal"
    };

    private static string Title(Type type)
    {
        return type.IsGenericTypeDefinition
            ? Bare(type.Name) + " of " + string.Join(", ", type.GetGenericArguments().Select(t => t.Name))
            : type.Name;
    }

    private static string CodeName(Type type)
    {
        return type.IsGenericTypeDefinition
            ? Bare(type.Name) + "<" + string.Join(", ", type.GetGenericArguments().Select(t => t.Name)) + ">"
            : type.Name;
    }

    private static string Bare(string name)
    {
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }

    private static bool Shown(MethodInfo method) =>
        method.IsPublic || (method.IsFamily || method.IsFamilyOrAssembly) && method.GetBaseDefinition() == method;

    private static bool Skip(MemberInfo member)
    {
        if (member.Name is "Equals" or "GetHashCode" or "ToString" or "Deconstruct" or "PrintMembers" or "GetType"
            or "op_Equality" or "op_Inequality" or "EqualityContract")
            return true;
        if (member.Name.StartsWith('<'))
            return true;
        return member.GetCustomAttribute<CompilerGeneratedAttribute>() is not null;
    }

    public static HashSet<Type> Included(Assembly[] assemblies)
    {
        var included = new HashSet<Type>();
        foreach (var type in assemblies.SelectMany(assembly => assembly.GetExportedTypes())
                     .Where(type => type is { IsPublic: true, IsNested: false }))
            if (type.Namespace is "Winshell" ||
                type.Namespace?.StartsWith("Winshell.", StringComparison.Ordinal) == true)
                included.Add(type);
        var queue = new Queue<Type>(included);
        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
                if (included.Add(nested))
                    queue.Enqueue(nested);
            foreach (var related in Related(type))
            {
                if (!assemblies.Contains(related.Assembly))
                    continue;
                if (related.IsGenericParameter || related.Name.StartsWith('<') || related.IsByRef || related.IsPointer)
                    continue;
                if (related is { IsPublic: false, IsNestedPublic: false })
                    continue;
                if (included.Add(related))
                    queue.Enqueue(related);
            }
        }

        return included;
    }

    private static IEnumerable<Type> Related(Type type)
    {
        if (type.BaseType is { } baseType)
            yield return baseType;
        var types = type.GetProperties(Declared).Select(property => property.PropertyType)
            .Concat(type.GetFields(Declared).Select(field => field.FieldType))
            .Concat(type.GetEvents(Declared).Select(evt => evt.EventHandlerType).OfType<Type>())
            .Concat(type.GetMethods(Declared).Select(method => method.ReturnType))
            .Concat(type.GetMethods(Declared).Concat<MethodBase>(type.GetConstructors(Declared))
                .SelectMany(method => method.GetParameters()).Select(parameter => parameter.ParameterType));
        foreach (var related in types.SelectMany(Atoms))
            yield return related;
    }

    private static IEnumerable<Type> Atoms(Type type)
    {
        if (type.GetElementType() is { } element)
            return Atoms(element);
        if (type.IsGenericType)
            return type.GetGenericArguments().SelectMany(Atoms);
        return (Type[])[type];
    }
}
