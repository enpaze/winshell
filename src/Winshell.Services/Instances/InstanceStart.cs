using System.ComponentModel;
using System.Diagnostics;
using Winshell.Native;

namespace Winshell;

internal sealed record InstanceLaunch(
    string Program,
    IReadOnlyList<string> Arguments,
    ShellProperties Properties,
    Log Log,
    Action<ShellInstance>? Prepare = null);

internal sealed class InstanceSession
{
    internal InstanceSession(ShellInstance instance, ShellIdentity identity, ShellArguments arguments, Log log)
    {
        Instance = instance;
        Identity = identity;
        Arguments = arguments;
        Log = log;
    }

    public ShellInstance Instance { get; }

    public ShellIdentity Identity { get; }

    public ShellArguments Arguments { get; }

    public Log Log { get; }

    public Exception? Failure { get; set; }

    public void Serve(IEventLoop loop, IInstanceControl control)
    {
        Instance.Listen(loop, control);
        Instance.Publish();
    }
}

internal static class InstanceStart
{
    public const int CouldNotStart = 1;

    public static int Run(InstanceLaunch launch, Action<InstanceSession> host)
    {
        var arguments = CommandLine.ReadProgram(launch.Arguments);
        if (arguments.Daemonize)
            return Detach(launch.Program, arguments.Detached) is { } error ? Reported(error) : 0;

        if (arguments.After is { } previous)
            Instances.WaitForExit(previous);

        var locations = ShellLocations.Current;
        var identity = ShellIdentity.Resolve(launch.Program, locations, launch.Properties);
        using var duplicate = arguments.NoDuplicate
            ? HeldMutex.TryClaim(HeldMutex.NoDuplicateName(locations, identity.ShellName))
            : default;
        if (arguments.NoDuplicate && (!duplicate.Acquired || IsRunning(locations, identity.ShellName)))
            return 0;

        using var oneShell = ShellReplacement.Claim(locations, arguments.After, out var blocked);
        if (blocked is not null)
            return Reported(blocked);

        var instance = ShellInstance.Open(identity.ShellName, launch.Program, launch.Log, arguments.Verbose);
        launch.Prepare?.Invoke(instance);
        launch.Log.Info("shell",
            $"Winshell {ProductVersion.Current.Text} on Windows {Environment.OSVersion.Version}, program \"{launch.Program}\"");
        launch.Log.Info("shell", $"Shell name: \"{identity.ShellName}\"");
        identity.CarryOver(launch.Log);
        launch.Log.Info("shell", $"Saving logs to \"{instance.Logs}\"");
        if (instance.OpenPipe() is { } pipeError)
        {
            instance.Abandon();
            return Reported(pipeError);
        }

        var session = new InstanceSession(instance, identity, arguments, launch.Log);
        try
        {
            host(session);
        }
        catch (Exception e)
        {
            instance.ReportFatal(e);
            throw;
        }
        finally
        {
            instance.MarkCleanExit();
            instance.Close();
        }

        return session.Failure is { } failure ? Reported(failure.Message) : 0;
    }

    private static bool IsRunning(ShellLocations locations, string shellName)
    {
        return locations.Instances.Running()
            .Any(info => string.Equals(info.ShellName, shellName, StringComparison.OrdinalIgnoreCase));
    }

    private static int Reported(string reason)
    {
        StartupError.Report(reason, null);
        return CouldNotStart;
    }

    internal static string? Detach(string program, IEnumerable<string> arguments)
    {
        try
        {
            using var none = File.OpenNullHandle();
            var start = DetachedStart(program, arguments);
            start.InheritedHandles = [];
            start.StandardInputHandle = none;
            start.StandardOutputHandle = none;
            start.StandardErrorHandle = none;
            using var child = Process.Start(start);
            return null;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return $"Could not start \"{program}\": {e.Message}";
        }
    }

    internal static ProcessStartInfo DetachedStart(string program, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(program) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        return info;
    }
}
