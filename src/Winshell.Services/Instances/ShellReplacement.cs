using System.ComponentModel;
using System.Diagnostics;

namespace Winshell;

internal static class ShellReplacement
{
    private static readonly TimeSpan StartLimit = TimeSpan.FromSeconds(20);

    internal static HeldMutex Claim(ShellLocations locations, int? leaving, out string? blocked)
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

            var hold = HeldMutex.TryClaim(HeldMutex.OneShellName(locations));
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
        return locations.Instances.Running()
            .Where(info => info.Pid != Environment.ProcessId && info.Pid != leaving)
            .ToList();
    }

    private static string? Ask(InstanceInfo info)
    {
        ExitIntentEvents.Ask(info.Pid, ExitIntent.Stop);
        using var process = Instances.OpenProcess(info);
        if (process is null)
            return null;

        IpcReply? reply = null;
        try
        {
#pragma warning disable VSTHRD002
            reply = Task.Run(async () =>
            {
                using var cancel = new CancellationTokenSource(QuitPatience.Interactive.Ask);
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
            ExitIntentEvents.Withdraw(info.Pid, ExitIntent.Stop);

        var why = reply switch
        {
            IpcFailure failure => failure.Sentence(info.Id),
            IpcResponse { Ok: false, Error: { } error } => error,
            _ => "it did not exit"
        };
        return $"Shell {info.ShellName} did not quit ({why}), so this one was not started.";
    }

    private static bool Exited(Process process)
    {
        try
        {
            return process.WaitForExit(QuitPatience.Interactive.Wait);
        }
        catch (Win32Exception)
        {
            return process.HasExited;
        }
    }

}
