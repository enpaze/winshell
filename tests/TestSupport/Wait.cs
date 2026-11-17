namespace Winshell.TestSupport;

internal static class Wait
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FirstPause = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan LongestPause = TimeSpan.FromMilliseconds(100);

    internal static void Until(Func<bool> done, string what, Action? between = null)
    {
        Until(done, what, Patience, between);
    }

    internal static void Until(Func<bool> done, string what, TimeSpan patience, Action? between = null)
    {
        var token = TestContext.Current.CancellationToken;
        var started = TimeProvider.System.GetTimestamp();
        var pause = FirstPause;
        while (!done())
        {
            Assert.True(TimeProvider.System.GetElapsedTime(started) < patience, $"Timed out: {what}");
            token.WaitHandle.WaitOne(pause);
            token.ThrowIfCancellationRequested();
            pause = Next(pause);
            between?.Invoke();
        }
    }

    internal static Task UntilAsync(Func<bool> done, string what, Action? between = null)
    {
        return UntilAsync(done, what, () =>
        {
            between?.Invoke();
            return Task.CompletedTask;
        });
    }

    internal static async Task UntilAsync(Func<bool> done, string what, Func<Task> between)
    {
        var token = TestContext.Current.CancellationToken;
        var started = TimeProvider.System.GetTimestamp();
        var pause = FirstPause;
        while (!done())
        {
            Assert.True(TimeProvider.System.GetElapsedTime(started) < Patience, $"Timed out: {what}");
            await Task.Delay(pause, token);
            pause = Next(pause);
            await between();
        }
    }

    internal static async Task UntilAsync(Func<Task<bool>> done, string what, TimeSpan? patience = null)
    {
        var token = TestContext.Current.CancellationToken;
        var started = TimeProvider.System.GetTimestamp();
        var pause = FirstPause;
        while (!await done())
        {
            Assert.True(TimeProvider.System.GetElapsedTime(started) < (patience ?? Patience), $"Timed out: {what}");
            await Task.Delay(pause, token);
            pause = Next(pause);
        }
    }

    internal static void ForNothingToHappen(TimeSpan time)
    {
        var token = TestContext.Current.CancellationToken;
        token.WaitHandle.WaitOne(time);
        token.ThrowIfCancellationRequested();
    }

    private static TimeSpan Next(TimeSpan pause)
    {
        return pause * 2 < LongestPause ? pause * 2 : LongestPause;
    }
}
