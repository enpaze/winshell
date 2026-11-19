using System.Text.Json.Serialization;

namespace Winshell;

internal static class IpcOps
{
    public const string Show = "show";
    public const string Call = "call";
    public const string Prop = "prop";
    public const string Wait = "wait";
    public const string Listen = "listen";
    public const string Stop = "stop";
    public const string Yield = "yield";
    public const string Reload = "reload";
    public const string Build = "build";
    public const string Inspect = "inspect";

    public static bool ChecksRelease(string op)
    {
        return op is not (Stop or Yield);
    }
}

internal sealed partial record IpcRequest
{
    public static IpcRequest ForShow(bool json)
    {
        return new IpcRequest(IpcOps.Show, Json: json);
    }

    public static IpcRequest ForCall(string target, string name, List<string> args)
    {
        return new IpcRequest(IpcOps.Call, target, name, args);
    }

    public static IpcRequest ForProp(string target, string name)
    {
        return new IpcRequest(IpcOps.Prop, target, name);
    }

    public static IpcRequest ForWait(string target, string name)
    {
        return new IpcRequest(IpcOps.Wait, target, name);
    }

    public static IpcRequest ForListen(string target, string name)
    {
        return new IpcRequest(IpcOps.Listen, target, name);
    }

    public static IpcRequest ForStop()
    {
        return new IpcRequest(IpcOps.Stop);
    }

    public static IpcRequest ForYield()
    {
        return new IpcRequest(IpcOps.Yield);
    }

    public static IpcRequest ForReload(string? program = null)
    {
        return new IpcRequest(IpcOps.Reload, Args: program is null ? null : [program]);
    }

    [JsonIgnore]
    public string? ReloadProgram => Args is { Count: > 0 } args ? args[0] : null;

    public static IpcRequest ForBuild(string error, string projectFile)
    {
        return new IpcRequest(IpcOps.Build, Args: [error, projectFile]);
    }

    [JsonIgnore]
    public (string Error, string ProjectFile)? BuildReport => Args is { Count: 2 } args ? (args[0], args[1]) : null;

    public static IpcRequest ForInspect(string command, List<string> args)
    {
        return new IpcRequest(IpcOps.Inspect, command, Args: args);
    }
}
