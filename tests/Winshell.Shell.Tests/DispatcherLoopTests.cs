namespace Winshell.ShellTests;

[TestClass]
public sealed class DispatcherLoopTests
{
    [TestMethod]
    public async Task A_timer_whose_handle_was_dropped_still_fires()
    {
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await ShellFixture.On(shell =>
        {
            shell.Loop.Schedule(TimeSpan.Zero, fired.SetResult);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return true;
        });

        await fired.Task.WaitAsync(TimeSpan.FromSeconds(10), CurrentTest.Cancellation);
    }
}
