using System.Globalization;
using System.Text;

namespace Winshell;

internal sealed record IpcParameterView(string Name, string? Type);

internal sealed record IpcFunctionView(string Name, IReadOnlyList<IpcParameterView> Parameters, string Returns);

internal sealed record IpcPropertyView(string Name, string Type);

internal sealed record IpcSignalView(string Name, IReadOnlyList<IpcParameterView> Parameters);

internal sealed record IpcTargetView(
    string Target,
    IReadOnlyList<IpcFunctionView> Functions,
    IReadOnlyList<IpcPropertyView> Properties,
    IReadOnlyList<IpcSignalView> Signals);

public sealed class IpcException(string message) : Exception(message);

internal static class IpcValues
{
    public const int MaxParameters = 10;

    public static string Show(this ShellIpc ipc)
    {
        var sb = new StringBuilder();
        foreach (var target in ipc.Describe())
        {
            sb.Append("target ").Append(target.Target).Append('\n');
            foreach (var function in target.Functions)
                sb.Append("  function ").Append(function.Name).Append('(')
                    .Append(string.Join(", ", function.Parameters.Select(p => $"{p.Name}: {p.Type}"))).Append("): ")
                    .Append(function.Returns).Append('\n');
            foreach (var property in target.Properties)
                sb.Append("  property ").Append(property.Name).Append(": ").Append(property.Type).Append('\n');
            foreach (var signal in target.Signals)
                sb.Append("  signal ").Append(signal.Name).Append('(')
                    .Append(string.Join(", ", signal.Parameters.Select(p => p.Name))).Append(")\n");
        }

        return sb.ToString();
    }

    public static string WrongTypes(string target, string function)
    {
        return
            $"Function {target}.{function} has a type IPC can't pass. It takes and returns string, int, bool, real, double or color.";
    }

    public static string WrongCount(string target, string function, int declared, int given)
    {
        return $"Function {target}.{function} takes {declared} argument{(declared == 1 ? "" : "s")}, {given} given.";
    }

    public static int ParseInt(string text, string name)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
            ? i
            : throw new IpcException($"Argument {name} is not an int: {text}.");
    }

    public static double ParseReal(string text, string name)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : throw new IpcException($"Argument {name} is not a real: {text}.");
    }

    public static bool ParseBool(string text, string name)
    {
        return text switch
        {
            "true" => true,
            "false" => false,
            _ when int.TryParse(text, out var n) => n != 0,
            _ => throw new IpcException($"Argument {name} is not a bool: {text}.")
        };
    }

    public static string ColorText(string text)
    {
        return text.StartsWith('#') || !IsHex(text) ? text : "#" + text;
    }

    private static bool IsHex(string s)
    {
        return s.Length is 3 or 6 or 8 && s.All(char.IsAsciiHexDigit);
    }
}
