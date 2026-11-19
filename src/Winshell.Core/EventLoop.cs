namespace Winshell;

internal interface IEventLoop
{
    void Post(Action action);

    IDisposable Schedule(TimeSpan delay, Action action);

    TimeProvider Time { get; }
}

internal static class EventLoop
{
    internal static IEventLoop Current
    {
        get => field ?? throw new InvalidOperationException("The shell has not started. Call Shell.Run first.");
        set;
    }
}
