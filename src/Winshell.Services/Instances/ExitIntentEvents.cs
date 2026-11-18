namespace Winshell;

internal enum ExitIntent
{
    None,

    Reload,

    Stop
}

internal sealed class ExitIntentEvents(int pid) : IDisposable
{
    private readonly EventWaitHandle _stop = new(false, EventResetMode.ManualReset, Name(ExitIntent.Stop, pid));

    private readonly EventWaitHandle _reload = new(false, EventResetMode.ManualReset, Name(ExitIntent.Reload, pid));

    public ExitIntent Intent =>
        _stop.WaitOne(0) ? ExitIntent.Stop : _reload.WaitOne(0) ? ExitIntent.Reload : ExitIntent.None;

    public static string Name(ExitIntent intent, int pid)
    {
        return intent == ExitIntent.Stop
            ? $@"Local\winshell-stop-asked-{pid}"
            : $@"Local\winshell-reload-asked-{pid}";
    }

    public static bool IsSupervised(int pid)
    {
        if (!EventWaitHandle.TryOpenExisting(Name(ExitIntent.Stop, pid), out var handle))
            return false;
        handle.Dispose();
        return true;
    }

    public static void Ask(int pid, ExitIntent intent)
    {
        if (!EventWaitHandle.TryOpenExisting(Name(intent, pid), out var handle))
            return;
        using (handle)
            handle.Set();
    }

    public static void Withdraw(int pid, ExitIntent intent)
    {
        if (!EventWaitHandle.TryOpenExisting(Name(intent, pid), out var handle))
            return;
        using (handle)
            handle.Reset();
    }

    public void Dispose()
    {
        _stop.Dispose();
        _reload.Dispose();
    }
}
