namespace Winshell;

internal sealed class SettleTimer(IEventLoop loop, Action settled, Func<bool>? live = null) : IDisposable
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(100);

    private IDisposable? _timer;
    private bool _disposed;

    public void Signal()
    {
        if (Live())
            loop.Post(Restart);
    }

    public void Cancel()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        _disposed = true;
        Cancel();
    }

    private void Restart()
    {
        if (!Live())
            return;
        _timer?.Dispose();
        _timer = loop.Schedule(Quiet, () =>
        {
            _timer = null;
            if (Live())
                settled();
        });
    }

    private bool Live()
    {
        return !_disposed && (live?.Invoke() ?? true);
    }
}
