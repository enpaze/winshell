namespace Winshell;

/// <summary>
/// Runs a Windows call that can block on a thread of its own. Not the thread pool: a blocked call holds its pool
/// thread, and when the pool is busy the call, and whatever waits on it, waits for a free one.
/// </summary>
internal static class OwnThread
{
    /// <summary>
    /// Runs <paramref name="call"/> on a new thread. An await of the task without a context continues on that thread,
    /// so what follows the call needs no pool thread either.
    /// </summary>
    public static Task<T> Run<T>(Func<T> call)
    {
        return Task.Factory.StartNew(call, CancellationToken.None,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
    }
}
