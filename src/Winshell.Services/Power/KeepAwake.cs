using Winshell.Native;

namespace Winshell.Power;

public sealed class KeepAwake : ObservableObject, IDisposable
{
    private static int _active;

    private readonly Log _log;
    private PowerRequest? _request;

    public KeepAwake(Log log)
    {
        _log = log;
    }

    public bool Enabled
    {
        get => Read(field);
        set
        {
            if (!Set(ref field, value))
                return;
            Interlocked.Add(ref _active, value ? 1 : -1);
            if (value)
                Acquire();
            else
                Release();
        }
    }

    internal static bool AnyActive => Volatile.Read(ref _active) > 0;

    private void Acquire()
    {
        _request = PowerRequest.TryCreate("Winshell keeps the screen on", out var step, out var error);
        if (_request is null)
            _log.Warn("power", $"Cannot {step} the power request: Windows error {error}");
    }

    private void Release()
    {
        _request?.Dispose();
        _request = null;
    }

    public void Dispose()
    {
        Enabled = false;
    }
}
