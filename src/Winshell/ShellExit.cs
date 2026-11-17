using System.ComponentModel;
using System.Diagnostics;

namespace Winshell;

internal enum ExitIntent
{
    None,

    Reload,

    Stop
}

internal static partial class ShellExit
{
    internal sealed record Patience(TimeSpan Ask, TimeSpan Wait)
    {
        public static readonly Patience Interactive =
            new(IpcClient.ConnectLimit + TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));

        public static readonly Patience Closing = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
    }

    internal enum Ending
    {
        Never,

        IfAccepted,

        Always
    }

    internal readonly record struct Outcome(bool Exited, InstanceCall.Answer Answer);

    internal sealed partial class Supervision(int pid) : IDisposable
    {
        private readonly EventWaitHandle
            _stop = new(false, EventResetMode.ManualReset, EventName(ExitIntent.Stop, pid));

        private readonly EventWaitHandle _reload = new(false, EventResetMode.ManualReset,
            EventName(ExitIntent.Reload, pid));

        public ExitIntent Intent =>
            _stop.WaitOne(0) ? ExitIntent.Stop : _reload.WaitOne(0) ? ExitIntent.Reload : ExitIntent.None;

        public void Dispose()
        {
            _stop.Dispose();
            _reload.Dispose();
        }
    }

    public static bool IsSupervised(int pid)
    {
        if (!EventWaitHandle.TryOpenExisting(EventName(ExitIntent.Stop, pid), out var handle))
            return false;
        handle.Dispose();
        return true;
    }

    public static async Task<Outcome> QuitAsync(InstanceInfo info, ExitIntent intent, Patience patience, Ending ending)
    {
        using var process = Open(info);
        return process is null ? new Outcome(true, default) : await QuitAsync(process, info, intent, patience, ending);
    }

    public static async Task<Outcome> QuitAsync(Process process, InstanceInfo? info, ExitIntent intent,
        Patience patience, Ending ending)
    {
        if (intent != ExitIntent.None)
            Tell(process.Id, intent, true);
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
                    answer = await InstanceCall.AskAsync(info, IpcRequest.ForKill(), deadline.Token);
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
                Tell(process.Id, intent, false);
        }
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

    private static void Tell(int pid, ExitIntent intent, bool asked)
    {
        if (!EventWaitHandle.TryOpenExisting(EventName(intent, pid), out var handle))
            return;
        using (handle)
        {
            if (asked)
                handle.Set();
            else
                handle.Reset();
        }
    }

    private static string EventName(ExitIntent intent, int pid)
    {
        return intent == ExitIntent.Stop
            ? SupervisionStop.EventName(pid)
            : $@"Local\winshell-reload-asked-{pid}";
    }
}
