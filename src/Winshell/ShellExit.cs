using System.ComponentModel;
using System.Diagnostics;

namespace Winshell;

internal static class ShellExit
{
    internal enum Ending
    {
        Never,

        IfAccepted,

        Always
    }

    internal readonly record struct Outcome(bool Exited, InstanceCall.Answer Answer);

    public static async Task<Outcome> QuitAsync(InstanceInfo info, ExitIntent intent, QuitPatience patience,
        Ending ending)
    {
        using var process = Instances.OpenProcess(info);
        return process is null ? new Outcome(true, default) : await QuitAsync(process, info, intent, patience, ending);
    }

    public static async Task<Outcome> QuitAsync(Process process, InstanceInfo? info, ExitIntent intent,
        QuitPatience patience, Ending ending)
    {
        if (intent != ExitIntent.None)
            ExitIntentEvents.Ask(process.Id, intent);
        var exited = false;
        try
        {
            InstanceCall.Answer answer = default;
            var late = false;
            if (info is not null)
            {
                using var deadline = new CancellationTokenSource(patience.Ask);
                try
                {
                    answer = await InstanceCall.AskAsync(info, IpcRequest.ForStop(), deadline.Token);
                }
                catch (OperationCanceledException)
                {
                    late = true;
                }
            }

            var end = ending == Ending.Always ||
                      (ending == Ending.IfAccepted && (late || answer.Response is { Ok: true }));
            exited = await WaitAsync(process, patience.Wait) || (end && await EndAsync(process, info));
            return new Outcome(exited, answer);
        }
        finally
        {
            if (intent != ExitIntent.None && !exited && !process.HasExited)
                ExitIntentEvents.Withdraw(process.Id, intent);
        }
    }

    private static async Task<bool> WaitAsync(Process process, TimeSpan timeout)
    {
        using var patience = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(patience.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return process.WaitForExit(timeout);
        }
    }

    private static async Task<bool> EndAsync(Process process, InstanceInfo? info)
    {
        try
        {
            process.Refresh();
            if (info is not null && !Instances.Wrote(info, process))
                return true;
            process.Kill();
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception)
        {
        }

        return await WaitAsync(process, TimeSpan.FromSeconds(1));
    }
}
