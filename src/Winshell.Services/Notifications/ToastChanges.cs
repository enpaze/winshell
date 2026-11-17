namespace Winshell.Notifications;

internal sealed class ToastChanges(Func<string?> stamp, TimeProvider time)
{
    internal static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(1);

    private string? _readStamp;
    private int _readChanges = -1;
    private long _readAt;
    private int _changes;

    public bool Due(out (string? Stamp, int Changes) current)
    {
        current = (stamp(), Volatile.Read(ref _changes));
        return current.Stamp is null || current.Stamp != _readStamp || current.Changes != _readChanges ||
               time.GetElapsedTime(_readAt) >= MaxAge;
    }

    public void Read((string? Stamp, int Changes) current)
    {
        _readStamp = current.Stamp;
        _readChanges = current.Changes;
        _readAt = time.GetTimestamp();
    }

    public void Change()
    {
        Interlocked.Increment(ref _changes);
    }
}
