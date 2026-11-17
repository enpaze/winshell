using System.ComponentModel;
using System.Diagnostics;

namespace Winshell;

internal static class ShellReplacement
{
    private static readonly TimeSpan AskLimit = IpcClient.ConnectLimit + TimeSpan.FromSeconds(2);

    private static readonly TimeSpan ExitLimit = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan StartLimit = TimeSpan.FromSeconds(20);

    internal static OneShellHold Claim(ShellLocations locations, int? leaving, out string? blocked)
    {
        var started = TimeProvider.System.GetTimestamp();
        while (true)
        {
            foreach (var info in Others(locations, leaving))
            {
                if (Ask(info) is { } reason)
                {
                    blocked = reason;
                    return default;
                }
            }

            if (Others(locations, leaving).Count > 0)
                continue;

            var hold = OneShellHold.TryClaim(locations);
            if (hold.Acquired)
            {
                if (Others(locations, leaving).Count == 0)
                {
                    blocked = null;
                    return hold;
                }

                hold.Dispose();
                continue;
            }

            if (leaving is int pid && Instances.ProcessAlive(pid))
            {
                blocked = null;
                return default;
            }

            if (TimeProvider.System.GetElapsedTime(started) > StartLimit)
            {
                blocked = "A shell is starting and did not quit, so this one was not started.";
                return default;
            }

            Thread.Sleep(50);
        }
    }

    private static List<InstanceInfo> Others(ShellLocations locations, int? leaving)
    {
        return locations.Instances.List(false).Select(row => row.Info)
            .Where(info => info.Pid != Environment.ProcessId && info.Pid != leaving)
            .ToList();
    }

    private static string? Ask(InstanceInfo info)
    {
        SupervisionStop.Ask(info.Pid);
        using var process = Open(info);
        if (process is null)
            return null;

        IpcReply? reply = null;
        try
        {
#pragma warning disable VSTHRD002
            reply = Task.Run(async () =>
            {
                using var cancel = new CancellationTokenSource(AskLimit);
                await foreach (var next in IpcClient.Request(info.Pipe, IpcRequest.ForYield(), cancel.Token)
                                   .ConfigureAwait(false))
                    return next;
                return (IpcReply?)null;
            }).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
        catch (Exception e) when (e is OperationCanceledException or AggregateException)
        {
        }

        if (Exited(process))
            return null;

        if (!process.HasExited)
            SupervisionStop.Withdraw(info.Pid);

        var why = reply switch
        {
            IpcFailure failure => failure.Sentence(info.Id),
            IpcResponse { Ok: false, Error: { } error } => error,
            _ => "it did not exit"
        };
        return $"Shell {info.ShellId} did not quit ({why}), so this one was not started.";
    }

    private static Process? Open(InstanceInfo info)
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(info.Pid);
            if (!process.HasExited && Instances.Wrote(info, process))
                return process;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception)
        {
        }

        process?.Dispose();
        return null;
    }

    private static bool Exited(Process process)
    {
        try
        {
            return process.WaitForExit(ExitLimit);
        }
        catch (Win32Exception)
        {
            return process.HasExited;
        }
    }

}
