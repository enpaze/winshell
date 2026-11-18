using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Media.Control;

namespace Winshell.Media;

internal sealed class SessionManagerPoll(
    IEventLoop loop,
    Log log,
    Func<IAsyncOperation<GlobalSystemMediaTransportControlsSessionManager>> request,
    Action<GlobalSystemMediaTransportControlsSessionManager> read) : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan FirstRequestTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan LongestRequestTimeout = TimeSpan.FromHours(1);

    private const string NotAnswering =
        "Windows' media session service (Now Playing Session Manager) is not answering, so no media session can be listed. Signing out, or restarting that service, brings it back.";

    private readonly FailureWarning _warning = new(log, "media");
    private IAsyncOperation<GlobalSystemMediaTransportControlsSessionManager>? _pending;
    private IDisposable? _refresh;
    private IDisposable? _timeout;
    private TimeSpan _requestTimeout = FirstRequestTimeout;
    private bool _refreshRequested;
    private bool _disposed;

    public void Refresh()
    {
        if (_disposed)
            return;
        if (_pending is not null)
        {
            _refreshRequested = true;
            return;
        }

        _refresh?.Dispose();
        _refresh = null;
        try
        {
            var operation = request();
            _pending = operation;
            _timeout = loop.Schedule(_requestTimeout, () => CancelExpired(operation));
            operation.Completed = (_, status) => loop.Post(() => Complete(operation, status));
        }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException)
        {
            _warning.Failed(e);
            if (_pending is null)
                ScheduleRefresh();
        }
    }

    private void CancelExpired(IAsyncOperation<GlobalSystemMediaTransportControlsSessionManager> operation)
    {
        if (!ReferenceEquals(_pending, operation))
            return;
        _warning.Failed(new TimeoutException(NotAnswering));
        _requestTimeout = _requestTimeout * 2 < LongestRequestTimeout ? _requestTimeout * 2 : LongestRequestTimeout;
        Retire(operation);
    }

    public void Dispose()
    {
        _disposed = true;
        _refresh?.Dispose();
        _refresh = null;
        if (_pending is { } operation)
            Retire(operation);
    }

    private void Retire(IAsyncOperation<GlobalSystemMediaTransportControlsSessionManager> operation)
    {
        try
        {
            operation.Cancel();
        }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            _warning.Failed(e);
        }

        Complete(operation, AsyncStatus.Canceled);
    }

    private void Complete(IAsyncOperation<GlobalSystemMediaTransportControlsSessionManager> operation,
        AsyncStatus status)
    {
        if (!ReferenceEquals(_pending, operation))
            return;
        _timeout?.Dispose();
        _timeout = null;
        if (status != AsyncStatus.Canceled)
            _requestTimeout = FirstRequestTimeout;
        try
        {
            if (status == AsyncStatus.Completed)
            {
                read(operation.GetResults());
                _warning.Succeeded();
            }
            else if (status == AsyncStatus.Error)
            {
                _warning.Failed(operation.ErrorCode);
            }
        }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            _warning.Failed(e);
        }
        finally
        {
            try
            {
                operation.Close();
            }
            catch (Exception e) when (e is COMException or UnauthorizedAccessException or InvalidOperationException)
            {
                _warning.Failed(e);
            }

            _pending = null;
            if (_refreshRequested)
            {
                _refreshRequested = false;
                loop.Post(Refresh);
            }
            else
            {
                ScheduleRefresh();
            }
        }
    }

    private void ScheduleRefresh()
    {
        if (!_disposed)
            _refresh = loop.Schedule(RefreshInterval, Refresh);
    }
}
