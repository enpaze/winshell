using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Windows.UI;

namespace Winshell;

public sealed class ShellIpc
{
    private static readonly Dictionary<Type, string> Declared = new()
    {
        [typeof(string)] = "string",
        [typeof(int)] = "int",
        [typeof(bool)] = "bool",
        [typeof(double)] = "double",
        [typeof(float)] = "real",
        [typeof(Color)] = "color",
        [typeof(void)] = "void"
    };

    private readonly Log _log;

    private readonly SortedDictionary<string, Target> _targets = new(StringComparer.Ordinal);

    internal ShellIpc(Log log)
    {
        _log = log;
    }

    public IDisposable Handle(string target, string function, Delegate handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);
        ArgumentException.ThrowIfNullOrEmpty(function);
        ArgumentNullException.ThrowIfNull(handler);
        var signature = handler.GetType().GetMethod("Invoke")!;
        var types = signature.GetParameters().Select(p => p.ParameterType).ToArray();
        if (types.Length > IpcValues.MaxParameters || types.Any(t => !Declared.ContainsKey(t)) ||
            !Declared.ContainsKey(signature.ReturnType))
            throw new ArgumentException(IpcValues.WrongTypes(target, function), nameof(handler));
        var names = handler.Method.GetParameters().TakeLast(types.Length).Select(p => p.Name ?? "").ToArray();
        return Add(target, function, new Function(function, names, types, signature.ReturnType, handler),
            t => t.Functions);
    }

    public IDisposable Property<T>(string target, string property, Func<T> read)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);
        ArgumentException.ThrowIfNullOrEmpty(property);
        ArgumentNullException.ThrowIfNull(read);
        if (!Declared.ContainsKey(typeof(T)))
            throw new ArgumentException($"Property {target}.{property} has a type IPC can't pass.", nameof(read));
        return Add(target, property, new Value(property, typeof(T), () => read()), t => t.Properties);
    }

    public IpcSignal Signal(string target, string signal)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);
        ArgumentException.ThrowIfNullOrEmpty(signal);
        var made = new IpcSignal(signal);
        Add(target, signal, made, t => t.Signals);
        return made;
    }

    internal IReadOnlyList<IpcTargetView> Describe()
    {
        return _targets.Select(pair => new IpcTargetView(
            pair.Key,
            pair.Value.Functions.Select(f => new IpcFunctionView(f.Name,
                f.Names.Zip(f.Types, (name, type) => new IpcParameterView(name, Declared[type])).ToList(),
                Declared[f.Returns])).ToList(),
            pair.Value.Properties.Select(p => new IpcPropertyView(p.Name, Declared[p.Type])).ToList(),
            pair.Value.Signals.Select(s => new IpcSignalView(s.Name, [])).ToList())).ToList();
    }

    internal string Call(string target, string function, IReadOnlyList<string> args)
    {
        var found = Find(target).Functions.Find(f => f.Name == function) ??
                    throw new IpcException($"Function not found: {target}.{function}.");
        if (args.Count != found.Types.Length)
            throw new IpcException(IpcValues.WrongCount(target, function, found.Types.Length, args.Count));
        var converted = new object[args.Count];
        for (var i = 0; i < args.Count; i++)
            converted[i] = Parse(args[i], found.Types[i], found.Names[i]);
        try
        {
            return Format(found.Handler.DynamicInvoke(converted));
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }

    internal string GetProperty(string target, string property)
    {
        var found = Find(target).Properties.Find(p => p.Name == property) ??
                    throw new IpcException($"Property not found: {target}.{property}.");
        return Format(found.Read());
    }

    internal IDisposable Listen(string target, string signal, Action<string> onSignal)
    {
        var found = Find(target).Signals.Find(s => s.Name == signal) ??
                    throw new IpcException($"Signal not found: {target}.{signal}.");
        return found.Connect(onSignal);
    }

    private Target Find(string target)
    {
        return _targets.TryGetValue(target, out var found)
            ? found
            : throw new IpcException($"Target not found: {target}.");
    }

    private DisposeAction Add<T>(string target, string name, T member, Func<Target, List<T>> list)
        where T : IIpcMember
    {
        if (!_targets.TryGetValue(target, out var found))
            _targets[target] = found = new Target();
        var members = list(found);
        var index = members.FindIndex(m => m.Name == name);
        if (index >= 0)
        {
            _log.Warn("ipc", $"Duplicate IPC name \"{target}.{name}\"; the later one wins.");
            members[index] = member;
        }
        else
        {
            members.Add(member);
        }

        return new DisposeAction(() =>
        {
            if (members.Remove(member) && found.IsEmpty)
                _targets.Remove(target);
        });
    }

    private static object Parse(string text, Type type, string name)
    {
        if (type == typeof(int))
            return IpcValues.ParseInt(text, name);
        if (type == typeof(double))
            return IpcValues.ParseReal(text, name);
        if (type == typeof(float))
            return (float)IpcValues.ParseReal(text, name);
        if (type == typeof(bool))
            return IpcValues.ParseBool(text, name);
        if (type == typeof(Color))
            return ParseColor(IpcValues.ColorText(text)) ??
                   throw new IpcException($"Argument {name} is not a color: {text}.");
        return text;
    }

    private static Color? ParseColor(string text)
    {
        var hex = text.StartsWith('#') ? text[1..] : "";
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return null;
        return hex.Length switch
        {
            3 => Color.FromArgb(255, (byte)(((v >> 8) & 0xf) * 17), (byte)(((v >> 4) & 0xf) * 17),
                (byte)((v & 0xf) * 17)),
            4 => Color.FromArgb((byte)(((v >> 12) & 0xf) * 17), (byte)(((v >> 8) & 0xf) * 17),
                (byte)(((v >> 4) & 0xf) * 17), (byte)((v & 0xf) * 17)),
            6 => Color.FromArgb(255, (byte)(v >> 16), (byte)(v >> 8), (byte)v),
            8 => Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v),
            _ => null
        };
    }

    private static string Format(object? value)
    {
        return value switch
        {
            null => "",
            bool b => b ? "true" : "false",
            Color c => $"#{c.A:x2}{c.R:x2}{c.G:x2}{c.B:x2}",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
    }

    private sealed record Function(string Name, string[] Names, Type[] Types, Type Returns, Delegate Handler)
        : IIpcMember;

    private sealed record Value(string Name, Type Type, Func<object?> Read) : IIpcMember;

    private sealed class Target
    {
        public List<Function> Functions { get; } = [];

        public List<Value> Properties { get; } = [];

        public List<IpcSignal> Signals { get; } = [];

        public bool IsEmpty => Functions.Count == 0 && Properties.Count == 0 && Signals.Count == 0;
    }
}

internal interface IIpcMember
{
    string Name { get; }
}

public sealed class IpcSignal : IIpcMember
{
    private readonly List<Action<string>> _listeners = [];

    internal IpcSignal(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public void Emit(string value = "")
    {
        foreach (var listener in _listeners.ToArray())
            listener(value);
    }

    internal IDisposable Connect(Action<string> listener)
    {
        _listeners.Add(listener);
        return new Connection(this, listener);
    }

    private sealed class Connection(IpcSignal signal, Action<string> listener) : IDisposable
    {
        public void Dispose()
        {
            signal._listeners.Remove(listener);
        }
    }
}
