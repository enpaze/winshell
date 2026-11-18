using System.Collections.Concurrent;

namespace Winshell.ShellTests;

[TestClass]
public sealed partial class EventLoopContractTests
{
    [TestMethod]
    public async Task Posted_work_runs_in_post_order()
    {
        await PostedWorkRunsInOrder(Manual());
    }

    [TestMethod]
    public async Task Posted_work_runs_in_post_order_on_the_dispatcher()
    {
        await OnDispatcher(PostedWorkRunsInOrder);
    }

    [TestMethod]
    public async Task A_disposed_timer_does_not_fire_and_one_that_is_kept_does()
    {
        await ADisposedTimerDoesNotFire(Manual());
    }

    [TestMethod]
    public async Task A_disposed_timer_does_not_fire_and_one_that_is_kept_does_on_the_dispatcher()
    {
        await OnDispatcher(ADisposedTimerDoesNotFire);
    }

    [TestMethod]
    public async Task Posted_work_that_throws_is_reported_and_later_work_runs()
    {
        await PostedWorkThatThrowsIsReported(Manual());
    }

    [TestMethod]
    [TestCategory("Window")]
    public async Task Posted_work_that_throws_is_reported_and_later_work_runs_on_the_dispatcher()
    {
        await OnDispatcher(PostedWorkThatThrowsIsReported);
    }

    [TestMethod]
    public void An_assertion_in_posted_work_fails_the_test_instead_of_being_reported()
    {
        var loop = new ManualEventLoop { Log = new Log { Sink = static (_, _, _) => { } } };
        var failed = new List<Exception>();
        loop.Failed += failed.Add;
        loop.Post(() => Assert.Fail("from the callback"));

        var thrown = Assert.ThrowsExactly<AssertFailedException>(loop.Process);

        Assert.Contains("from the callback", thrown.ToString(), StringComparison.Ordinal);
        Assert.IsEmpty(failed);
    }

    [TestMethod]
    public void An_assertion_exception_outside_the_framework_namespace_fails_the_test()
    {
        var loop = new ManualEventLoop { Log = new Log { Sink = static (_, _, _) => { } } };
        var failed = new List<Exception>();
        loop.Failed += failed.Add;
        loop.Post(static () => throw new PostedAssertion());

        var thrown = Assert.ThrowsExactly<PostedAssertion>(loop.Process);

        Assert.AreEqual("from the callback", thrown.Message);
        Assert.IsEmpty(failed);
    }

    [TestMethod]
    public void A_queue_that_keeps_posting_work_fails_when_the_turn_cap_is_hit()
    {
        var loop = new ManualEventLoop { Log = new Log { Sink = static (_, _, _) => { } } };
        void KeepPosting() => loop.Post(KeepPosting);
        loop.Post(KeepPosting);

        var thrown = Assert.ThrowsExactly<InvalidOperationException>(loop.Process);

        Assert.AreEqual("The event loop hit its turn cap of 1000.", thrown.Message);
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

        Assert.AreSequenceEqual<int>([1, 2, 3], order);
    }

    private static async Task ADisposedTimerDoesNotFire(LoopRun run)
    {
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

        Assert.AreEqual(0, Volatile.Read(ref cancelled));
        Assert.AreEqual(1, Volatile.Read(ref kept));
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
                Interlocked.Exchange(ref ran, 1);
                finished.SetResult();
            });
            await run.Settle(TimeSpan.Zero, finished.Task);
            Assert.AreEqual(1, Volatile.Read(ref ran));
        }

        Assert.AreEqual("posted", Assert.ContainsSingle(failed).Message);
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
            Assert.IsTrue(done.IsCompleted, "The test loop did not finish the work.");
            return done;
        });
    }

    private static Task OnDispatcher(Func<LoopRun, Task> check)
    {
        return ShellFixture.On(async shell =>
        {
            var loop = Assert.IsExactInstanceOfType<DispatcherLoop>(shell.Loop);
            var run = new LoopRun(loop, handler =>
            {
                loop.Failed += handler;
                return new Detach(() => loop.Failed -= handler);
            }, (_, done) => done.WaitAsync(CurrentTest.Cancellation));
            try
            {
                await check(run);
            }
            finally
            {
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

    private sealed class PostedAssertion() : AssertFailedException("from the callback");
}
