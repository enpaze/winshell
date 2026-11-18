using System.Collections.Concurrent;

namespace Winshell.TestSupport;

internal partial class EventQueue(TimeProvider time) : IEventLoop
{
    private readonly ConcurrentQueue<Action> _posted = new();
    private readonly Queue<Action> _batch = new();
    private readonly PriorityQueue<TimerEntry, (TimeSpan, long)> _timers = new();
    private readonly long _started = time.GetTimestamp();
    private long _sequence;
    private int _cancelled;

    public EventQueue() : this(TimeProvider.System)
    {
    }

    public event Action? Woken;

    public TimeProvider Time => time;

    public TimeSpan Now => time.GetElapsedTime(_started);

    public Log Log { get; init; } = Log.Default;

    public event Action<Exception>? Failed;

    public void Post(Action action)
    {
        _posted.Enqueue(action);
        Woken?.Invoke();
    }

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var entry = new TimerEntry(this, action);
        lock (_timers)
        {
            _timers.Enqueue(entry, (Now + delay, _sequence++));
        }

        Woken?.Invoke();
        return entry;
    }

    public TimeSpan? NextDue
    {
        get
        {
            lock (_timers)
            {
                while (_timers.TryPeek(out var entry, out var due))
                {
                    if (!entry.Cancelled)
                        return due.Item1;
                    _timers.Dequeue();
                    _cancelled--;
                }

                return null;
            }
        }
    }

    public bool RunDue()
    {
        var ran = RunPosted();

        var now = Now;
        var dueNow = new List<TimerEntry>();
        lock (_timers)
        {
            while (_timers.TryPeek(out var entry, out var due) && due.Item1 <= now)
            {
                _timers.Dequeue();
                if (entry.Cancelled)
                {
                    _cancelled--;
                    continue;
                }

                entry.Cancelled = true;
                dueNow.Add(entry);
            }
        }

        foreach (var next in dueNow)
        {
            Run(next.Action);
            ran = true;
        }

        if (HasQueuedWork(now))
            Woken?.Invoke();
        return ran;
    }

    private bool RunPosted()
    {
        if (_batch.Count == 0)
            while (_posted.TryDequeue(out var queued))
                _batch.Enqueue(queued);
        var ran = _batch.Count > 0;
        while (_batch.TryDequeue(out var action))
            Run(action);
        return ran;
    }

    private bool HasQueuedWork(TimeSpan now)
    {
        if (!_posted.IsEmpty)
            return true;
        lock (_timers)
        {
            while (_timers.TryPeek(out var entry, out var due))
            {
                if (!entry.Cancelled)
                    return due.Item1 <= now;
                _timers.Dequeue();
                _cancelled--;
            }
        }

        return false;
    }

    private void Cancel(TimerEntry entry)
    {
        lock (_timers)
        {
            if (entry.Cancelled)
                return;
            entry.Cancelled = true;
            entry.DropAction();
            if (++_cancelled <= _timers.Count / 2)
                return;
            var live = _timers.UnorderedItems.Where(item => !item.Element.Cancelled).ToList();
            _timers.Clear();
            _timers.EnqueueRange(live);
            _cancelled = 0;
        }
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
#pragma warning disable CA1031
        catch (Exception e) when (!IsAssertion(e))
#pragma warning restore CA1031
        {
            Log.Error("loop", e.ToString());
            Failed?.Invoke(e);
        }
    }

    private static bool IsAssertion(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is UnitTestAssertException)
                return true;

        return false;
    }

    private sealed partial class TimerEntry(EventQueue queue, Action action) : IDisposable
    {
        private static readonly Action Dropped = static () => { };

        public Action Action { get; private set; } = action;

        public bool Cancelled { get; set; }

        public void DropAction()
        {
            Action = Dropped;
        }

        public void Dispose()
        {
            queue.Cancel(this);
        }
    }
}

internal sealed class ManualEventLoop : EventQueue
{
    private const int TurnCap = 1000;

    private readonly FakeTimeProvider _clock;

    public ManualEventLoop() : this(new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)))
    {
    }

    private ManualEventLoop(FakeTimeProvider clock) : base(clock)
    {
        _clock = clock;
    }

    public void Process()
    {
        for (var i = 0; i < TurnCap; i++)
            if (!RunDue())
                return;

        throw new InvalidOperationException($"The event loop hit its turn cap of {TurnCap}.");
    }

    public void Advance(TimeSpan by)
    {
        var end = Now + by;
        Process();
        while (NextDue is { } due && due <= end)
        {
            MoveTo(due);
            Process();
        }

        MoveTo(end);
        Process();
    }

    private void MoveTo(TimeSpan at)
    {
        if (at > Now)
            _clock.Advance(at - Now);
    }
}
