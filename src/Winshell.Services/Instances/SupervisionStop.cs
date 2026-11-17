namespace Winshell;

internal static class SupervisionStop
{
    internal static string EventName(int pid)
    {
        return $@"Local\winshell-stop-asked-{pid}";
    }

    internal static void Ask(int pid)
    {
        if (!EventWaitHandle.TryOpenExisting(EventName(pid), out var handle))
            return;
        using (handle)
            handle.Set();
    }

    internal static void Withdraw(int pid)
    {
        if (!EventWaitHandle.TryOpenExisting(EventName(pid), out var handle))
            return;
        using (handle)
            handle.Reset();
    }
}
