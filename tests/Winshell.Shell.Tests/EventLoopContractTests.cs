using System.Collections.Concurrent;
using Xunit.Sdk;

namespace Winshell.ShellTests;

/// <summary>
/// Ordering, timer cancellation and failure reporting, once each for the test loop and for
/// <see cref="DispatcherLoop"/>. The dispatcher runs on the shared real shell.
/// </summary>
public sealed partial class EventLoopContractTests
{
    [Fact]
    public async Task Posted_work_runs_in_post_order()
    {
        await PostedWorkRunsInOrder(Manual());
    }

    [Fact]
    public async Task Posted_work_runs_in_post_order_on_the_dispatcher()
    {
        await OnDispatcher(PostedWorkRunsInOrder);
    }

    [Fact]
    public async Task A_disposed_timer_does_not_fire_and_one_that_is_kept_does()
    {
        await ADisposedTimerDoesNotFire(Manual());
    }

    [Fact]
    public async Task A_disposed_timer_does_not_fire_and_one_that_is_kept_does_on_the_dispatcher()
    {
        await OnDispatcher(ADisposedTimerDoesNotFire);
    }

    [Fact]
    public async Task Posted_work_that_throws_is_reported_and_later_work_runs()
    {
        await PostedWorkThatThrowsIsReported(Manual());
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task Posted_work_that_throws_is_reported_and_later_work_runs_on_the_dispatcher()
    {
        await OnDispatcher(PostedWorkThatThrowsIsReported);
    }

    [Fact]
    public void An_assertion_in_posted_work_fails_the_test_instead_of_being_reported()
    {
        var loop = new ManualEventLoop { Log = new Log { Sink = static (_, _, _) => { } } };
        var failed = new List<Exception>();
        loop.Failed += failed.Add;
        loop.Post(() => Assert.Fail("from the callback"));

        var thrown = Assert.Throws<FailException>(loop.Process);

        Assert.Contains("from the callback", thrown.ToString(), StringComparison.Ordinal);
        Assert.Empty(failed);
    }

    private static async Task PostedWorkRunsInOrder(LoopRun run)
    {
        var order = new ConcurrentQueue<int>();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        run.Loop.Post(() =>
        {
            order.Enqueue(1);
            run.Loop.Post(() =>
            {
                order.Enqueue(3);
                finished.SetResult();
            });
        });
        run.Loop.Post(() => order.Enqueue(2));

        await run.Settle(TimeSpan.Zero, finished.Task);

        Assert.Equal([1, 2, 3], order);
    }

    private static async Task ADisposedTimerDoesNotFire(LoopRun run)
    {
        // The callbacks write these counters, and the test reads them after Settle.
        var cancelled = 0;
        var kept = 0;
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = run.Loop.Schedule(TimeSpan.FromMilliseconds(20), () => Interlocked.Exchange(ref cancelled, 1));
        run.Loop.Schedule(TimeSpan.FromMilliseconds(50), () =>
        {
            Interlocked.Exchange(ref kept, 1);
            finished.SetResult();
        });
        timer.Dispose();

        await run.Settle(TimeSpan.FromMilliseconds(50), finished.Task);

        Assert.Equal(0, Volatile.Read(ref cancelled));
        Assert.Equal(1, Volatile.Read(ref kept));
    }

    private static async Task PostedWorkThatThrowsIsReported(LoopRun run)
    {
        var failed = new ConcurrentQueue<Exception>();
        using (run.Listen(failed.Enqueue))
        {
            var ran = 0;
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            run.Loop.Post(() => throw new InvalidOperationException("posted"));
            run.Loop.Post(() =>
            {
                // The callback sets ran, and the assertion reads it after Settle.
                Interlocked.Exchange(ref ran, 1);
                finished.SetResult();
            });
            await run.Settle(TimeSpan.Zero, finished.Task);
            Assert.Equal(1, Volatile.Read(ref ran));
        }

        Assert.Equal("posted", Assert.Single(failed).Message);
    }

    private static LoopRun Manual()
    {
        var loop = new ManualEventLoop { Log = new Log { Sink = static (_, _, _) => { } } };
        return new LoopRun(loop, handler =>
        {
            loop.Failed += handler;
            return new Detach(() => loop.Failed -= handler);
        }, (delay, done) =>
        {
            loop.Advance(delay);
            Assert.True(done.IsCompleted, "The test loop did not finish the work.");
            return done;
        });
    }

    private static Task OnDispatcher(Func<LoopRun, Task> check)
    {
        return ShellFixture.On(async shell =>
        {
            var loop = Assert.IsType<DispatcherLoop>(shell.Loop);
            var run = new LoopRun(loop, handler =>
            {
                loop.Failed += handler;
                return new Detach(() => loop.Failed -= handler);
            }, (_, done) => done.WaitAsync(TestContext.Current.CancellationToken));
            try
            {
                await check(run);
            }
            finally
            {
                // The shell posts the crash notice after the report is flushed, which can sit behind this wait.
                // Post the hide here so it runs after that show. A nested On waits on the gate this turn holds.
                var hidden = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                shell.Loop.Post(() =>
                {
                    shell.Notices.Hide();
                    hidden.TrySetResult();
                });
                await hidden.Task;
            }

            return true;
        });
    }

    private readonly record struct LoopRun(
        IEventLoop Loop,
        Func<Action<Exception>, IDisposable> Listen,
        Func<TimeSpan, Task, Task> Settle);

    private sealed partial class Detach(Action detach) : IDisposable
    {
        public void Dispose()
        {
            detach();
        }
    }
}
