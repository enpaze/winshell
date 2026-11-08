namespace Winshell.ShellTests;

public sealed class DispatcherLoopTests
{
    [Fact]
    public async Task A_timer_whose_handle_was_dropped_still_fires()
    {
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await ShellFixture.On(shell =>
        {
            shell.Loop.Schedule(TimeSpan.Zero, fired.SetResult);
            // The dispatcher cannot fire until this turn yields, so collect while the dropped handle is still pending.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return true;
        });

        await fired.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }
}
