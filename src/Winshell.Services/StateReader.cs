namespace Winshell;

internal sealed class StateReader<T>(
    Func<CancellationToken, Task<T>> read,
    Action<T> apply,
    IEventLoop loop,
    FailureWarning warning)
{
    private readonly Lock _gate = new();

    private readonly CancellationTokenSource _stopping = new();

    private TaskCompletionSource? _next;
    private bool _reading;
    private int _stopRequested;
    private bool _stopped;
    private Action? _idle;

    private long _revision;

    public void Request()
    {
        _ = ReadAsync();
    }

    public Task ReadAsync()
    {
        TaskCompletionSource next;
        bool start;
        lock (_gate)
        {
            if (Volatile.Read(ref _stopRequested) != 0)
                return Task.CompletedTask;
            next = _next ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            start = !_reading;
            _reading = true;
        }

        if (start)
            _ = RunAsync();
        return next.Task;
    }

    public void Invalidate()
    {
        Interlocked.Increment(ref _revision);
    }

    public void Stop(Action? idle = null)
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 1)
            return;
        _stopping.Cancel();
        TaskCompletionSource? waiting;
        bool now;
        lock (_gate)
        {
            _stopped = true;
            Invalidate();
            waiting = _next;
            _next = null;
            now = !_reading;
            if (!now)
                _idle = idle;
        }

        waiting?.TrySetResult();
        if (now)
            Close(idle);
    }

    private async Task RunAsync()
    {
        TaskCompletionSource? finished = null;
        while (true)
        {
            TaskCompletionSource? next;
            Action? idle = null;
            bool stopped;
            lock (_gate)
            {
                stopped = _stopped;
                next = stopped ? null : _next;
                _next = null;
                if (next is null)
                {
                    _reading = false;
                    idle = _idle;
                    _idle = null;
                }
            }

            if (next is null)
            {
                try
                {
                    if (stopped)
                        Close(idle);
                }
                finally
                {
                    finished?.TrySetResult();
                }

                return;
            }

            finished?.TrySetResult();
            finished = next;
            await ReadOnceAsync().ConfigureAwait(false);
        }
    }

    private async Task ReadOnceAsync()
    {
        var revision = Interlocked.Read(ref _revision);
        T reading;
        try
        {
            reading = await read(_stopping.Token).ConfigureAwait(false);
            warning.Succeeded();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            warning.Failed(e);
            return;
        }

        loop.Post(() =>
        {
            if (revision == Interlocked.Read(ref _revision))
                apply(reading);
        });
    }

    private void Close(Action? idle)
    {
        try
        {
            idle?.Invoke();
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            warning.Failed(e);
        }
        finally
        {
            _stopping.Dispose();
        }
    }
}
