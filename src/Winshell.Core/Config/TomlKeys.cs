using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Winshell;

/// <summary>
/// The TOML keys of an object: one per public read-write property, the key being the property's name with a lowercase
/// first letter. It loads a table read from a file into those properties, as <see cref="TomlFile"/> describes.
/// </summary>
internal sealed class TomlKeys
{
    private readonly object _owner;
    private readonly Func<string, object, string>? _check;
    private readonly Action<string> _warn;

    /// <param name="owner">The object whose properties are the keys.</param>
    /// <param name="check">Returns why a value read for a key is refused, or <c>""</c>.</param>
    /// <param name="warn">Takes a warning about a value that was left, or a key that may be misspelled.</param>
    public TomlKeys(object owner, Func<string, object, string>? check, Action<string> warn)
    {
        _owner = owner;
        _check = check;
        _warn = warn;
        All =
        [
            .. owner.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.MetadataToken)
                .Select(p => new TomlKey(p))
        ];
    }

    /// <summary>The keys, in the order the type declares them.</summary>
    public IReadOnlyList<TomlKey> All { get; }

    /// <summary>Whether <see cref="Apply"/> is setting properties, so a change it makes is not taken for an assignment.</summary>
    public bool Loading { get; private set; }

    /// <summary>The key of the property named <paramref name="property"/>, or null when it is not one.</summary>
    public TomlKey? Of(string? property)
    {
        foreach (var key in All)
            if (key.Property.Name == property)
                return key;
        return null;
    }

    /// <summary>
    /// Loads <paramref name="values"/> into the properties. One that does not fit its property, or is refused, is
    /// reported and left. So is a name no key reads that looks like a misspelling of one.
    /// </summary>
    /// <param name="values">The table read from the file.</param>
    /// <param name="where">Says where a name of <paramref name="values"/> is in the file, as <c>path[line:column]</c>.</param>
    /// <param name="keep">Keys left as they are, such as ones assigned and not written yet.</param>
    /// <param name="defaults">
    /// What a key <paramref name="values"/> does not have goes back to, or null to leave such a key as it is.
    /// </param>
    public void Apply(IReadOnlyDictionary<string, object?> values, Func<string, string> where,
        IReadOnlySet<TomlKey>? keep = null, IReadOnlyDictionary<TomlKey, object?>? defaults = null)
    {
        foreach (var name in values.Keys)
            if (!All.Any(key => key.Name == name) && Nearest(name) is { } near)
                _warn($"{where(name)}: {name} was ignored; did you mean {near}?");
        Loading = true;
        try
        {
            foreach (var key in All)
            {
                if (keep?.Contains(key) == true)
                    continue;
                if (!values.TryGetValue(key.Name, out var value))
                {
                    if (defaults is not null && defaults.TryGetValue(key, out var fallback) &&
                        !TomlDocument.Same(TomlDocument.Plain(key.Value(_owner)), TomlDocument.Plain(fallback)))
                        key.Property.SetValue(_owner, fallback);
                    continue;
                }

                if (!TryConvert(value!, key.Property.PropertyType, out var converted))
                    Leave(where(key.Name), key, $"should be {Expected(key.Property.PropertyType)}, not {Shown(value!)}");
                else if (_check?.Invoke(key.Name, converted) is { Length: > 0 } reason)
                    Leave(where(key.Name), key, reason);
                else if (!TomlDocument.Same(TomlDocument.Plain(key.Value(_owner)), value))
                    key.Property.SetValue(_owner, converted);
            }
        }
        finally
        {
            Loading = false;
        }
    }

    /// <summary>The key <paramref name="name"/> may be a misspelling of: one that differs in case or in a letter or two.</summary>
    private string? Nearest(string name)
    {
        var best = (Name: (string?)null, Distance: 3);
        foreach (var key in All)
        {
            var distance = Distance(name.ToLowerInvariant(), key.Name.ToLowerInvariant());
            if (distance < best.Distance && (distance == 0 || distance < Math.Max(name.Length, key.Name.Length) / 2))
                best = (key.Name, distance);
        }

        return best.Name;
    }

    /// <summary>The fewest letters to add, remove or change to turn one word into the other.</summary>
    private static int Distance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1], previous[j]) + 1,
                    previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            previous = current;
        }

        return previous[b.Length];
    }

    private void Leave(string where, TomlKey key, string reason)
    {
        _warn($"{where}: {key.Name} {reason}, so it was left unchanged");
    }

    /// <summary>What a property of <paramref name="type"/> takes, for a warning about a value it does not.</summary>
    private static string Expected(Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            return "a list of " + Plural(target.GetGenericArguments()[0]);
        if (target == typeof(bool))
            return "true or false";
        if (target == typeof(int))
            return string.Create(CultureInfo.InvariantCulture, $"a whole number from {int.MinValue} to {int.MaxValue}");
        if (target == typeof(long))
            return "a whole number";
        if (target == typeof(double))
            return "a number";
        return target == typeof(string) ? "a string" : "a table";
    }

    private static string Plural(Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target == typeof(bool))
            return "true or false values";
        if (target == typeof(int))
            return string.Create(CultureInfo.InvariantCulture, $"whole numbers from {int.MinValue} to {int.MaxValue}");
        if (target == typeof(long))
            return "whole numbers";
        if (target == typeof(double))
            return "numbers";
        if (target == typeof(string))
            return "strings";
        return target.IsGenericType && target.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            ? "lists"
            : "tables";
    }

    /// <summary>A value read from the file, as TOML, cut short past 100 characters.</summary>
    private static string Shown(object value)
    {
        var text = TomlDocument.Format(value) ?? "";
        if (text.Length <= 100)
            return text;
        return text[..(char.IsHighSurrogate(text[99]) ? 99 : 100)] + "…";
    }

    private static bool TryConvert(object value, Type type, [NotNullWhen(true)] out object? converted)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        converted = null;
        if (target.IsInstanceOfType(value))
        {
            converted = value;
        }
        else if (target == typeof(bool))
        {
            converted = value switch
            {
                long number => number != 0,
                double number => number != 0 && !double.IsNaN(number),
                _ => null
            };
        }
        else if (target == typeof(int) && value is long and >= int.MinValue and <= int.MaxValue)
        {
            converted = (int)(long)value;
        }
        else if (target == typeof(double) && value is long whole)
        {
            converted = (double)whole;
        }
        else if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) &&
                 value is IReadOnlyList<object?> items)
        {
            var element = target.GetGenericArguments()[0];
            var list = Array.CreateInstance(element, items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                if (!TryConvert(items[i]!, element, out var item))
                    return false;
                list.SetValue(item, i);
            }

            converted = list;
        }

        return converted is not null;
    }
}

/// <summary>One key: a property and the comment written above it when the file does not have it yet.</summary>
internal sealed class TomlKey(PropertyInfo property)
{
    public PropertyInfo Property { get; } = property;

    public string Name { get; } = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];

    public string? Comment { get; } = property.GetCustomAttribute<TomlCommentAttribute>()?.Text;

    /// <summary>The property's value on <paramref name="owner"/>.</summary>
    public object? Value(object owner)
    {
        return Property.GetValue(owner);
    }
}
