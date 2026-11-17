namespace Winshell;

internal sealed class DeviceAction(Action<Action> post)
{
    private int _running;

    public bool Running => Volatile.Read(ref _running) != 0;

    public async Task RunAsync(Action start, Func<Task<string?>> work, Action<string> done, Action? ended = null)
    {
        if (Interlocked.Exchange(ref _running, 1) != 0)
            return;
        string error;
        try
        {
            start();
            error = await work().ConfigureAwait(false) ?? "";
        }
        catch (Exception e) when (WindowsFailure.IsExpected(e))
        {
            error = e.Message;
        }
#pragma warning disable CA1031
        catch (Exception)
        {
            End(null);
            throw;
        }
#pragma warning restore CA1031

        End(error);
        return;

        void End(string? reported)
        {
            post(() =>
            {
                Volatile.Write(ref _running, 0);
                ended?.Invoke();
                if (reported is not null)
                    done(reported);
            });
        }
    }
}
