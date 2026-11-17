namespace Winshell;

internal sealed class DisposeAction(Action action) : IDisposable
{
    private Action? _action = action;

    public void Dispose()
    {
        Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}
