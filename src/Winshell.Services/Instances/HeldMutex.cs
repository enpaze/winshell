namespace Winshell;

/// <summary>
/// A named local mutex owned by this thread until disposed. A failed claim owns nothing. A mutex a crashed owner
/// abandoned is claimed.
/// </summary>
internal readonly struct HeldMutex : IDisposable
{
    private readonly Mutex? _mutex;

    private HeldMutex(Mutex mutex)
    {
        _mutex = mutex;
    }

    public bool Acquired => _mutex is not null;

    /// <summary>Claims <paramref name="name"/>, or owns nothing when another owner holds it.</summary>
    public static HeldMutex TryClaim(string name)
    {
        var mutex = new Mutex(false, name);
        bool mine;
        try
        {
            mine = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner crashed while it held this. The wait succeeded and this caller owns it.
            mine = true;
        }

        if (!mine)
        {
            mutex.Dispose();
            return default;
        }

        return new HeldMutex(mutex);
    }

    public void Dispose()
    {
        if (_mutex is null)
            return;
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
