namespace Winshell;

public interface IEventLoop
{
    void Post(Action action);

    IDisposable Schedule(TimeSpan delay, Action action);

    TimeProvider Time { get; }
}
