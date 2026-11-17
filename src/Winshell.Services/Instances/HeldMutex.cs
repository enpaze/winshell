namespace Winshell;

internal readonly struct HeldMutex : IDisposable
{
    private readonly Mutex? _mutex;

    private HeldMutex(Mutex mutex)
    {
        _mutex = mutex;
    }

    public bool Acquired => _mutex is not null;

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
