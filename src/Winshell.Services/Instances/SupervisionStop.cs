namespace Winshell;

/// <summary>
/// The event a supervisor reads to learn that its shell was asked to stop. The supervisor creates it. A start that
/// replaces the shell sets it, so a crash on the way out still ends supervision instead of starting the shell again.
/// A shell that stays running has the mark taken back, so a later crash starts it again.
/// </summary>
internal static class SupervisionStop
{
    /// <summary>The event for the shell whose process id is <paramref name="pid"/>.</summary>
    internal static string EventName(int pid)
    {
        return $@"Local\winshell-stop-asked-{pid}";
    }

    /// <summary>Records that the shell was asked to stop, when it has a supervisor. A shell with none is unchanged.</summary>
    internal static void Ask(int pid)
    {
        if (!EventWaitHandle.TryOpenExisting(EventName(pid), out var handle))
            return;
        using (handle)
            handle.Set();
    }

    /// <summary>Forgets a stop the shell did not take. A later crash of that process starts the shell again.</summary>
    internal static void Withdraw(int pid)
    {
        if (!EventWaitHandle.TryOpenExisting(EventName(pid), out var handle))
            return;
        using (handle)
            handle.Reset();
    }
}
