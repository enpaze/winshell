namespace Winshell;

/// <summary>
/// One action at a time on a device or service. A call while one runs does nothing, and the result reaches the
/// loop through <c>post</c>.
/// </summary>
internal sealed class DeviceAction(Action<Action> post)
{
    private int _running;

    public bool Running => Volatile.Read(ref _running) != 0;

    /// <summary>
    /// Calls <paramref name="start"/>, then <paramref name="work"/>, unless an action is running. When
    /// <c>post</c> runs its callback, <see cref="Running"/> ends and <paramref name="ended"/> runs.
    /// On success or an expected Windows failure, <paramref name="done"/> then gets the error: what the work
    /// returned, or the text of that failure, empty after success. Any other exception faults the task this
    /// returns and does not call <paramref name="done"/>.
    /// </summary>
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
#pragma warning disable CA1031 // Running must end without reporting an empty success, then the task faults.
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
