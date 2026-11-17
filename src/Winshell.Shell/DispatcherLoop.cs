using Microsoft.UI.Dispatching;

namespace Winshell;

internal sealed partial class DispatcherLoop(DispatcherQueue queue, Log log) : IEventLoop
{
    private readonly HashSet<DispatcherQueueTimer> _timers = [];

    public TimeProvider Time => TimeProvider.System;

    public event Action<Exception>? Failed;

    public void Post(Action action)
    {
        queue.TryEnqueue(() => Run(action));
    }

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var timer = queue.CreateTimer();
        timer.IsRepeating = false;
        timer.Interval = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        lock (_timers)
        {
            _timers.Add(timer);
        }

        timer.Tick += (_, _) =>
        {
            Forget(timer);
            Run(action);
        };
        timer.Start();
        return new Timer(this, timer);
    }

    private void Forget(DispatcherQueueTimer timer)
    {
        timer.Stop();
        lock (_timers)
        {
            _timers.Remove(timer);
        }
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            log.Error("shell", e.ToString());
            Failed?.Invoke(e);
        }
    }

    private sealed partial class Timer(DispatcherLoop loop, DispatcherQueueTimer timer) : IDisposable
    {
        public void Dispose()
        {
            loop.Forget(timer);
        }
    }
}
