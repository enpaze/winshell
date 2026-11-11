using Winshell.Winland;

namespace Winshell.Services.Tests;

/// <summary>Looking for Winland waits longer each time it is not there, and never more often than every 30 s.</summary>
public sealed class QuietPollingTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    [Fact]
    public async Task Looking_for_winland_backs_off_from_3_to_30_seconds()
    {
        var clock = new Clock();
        var backend = new WinlandBackend(clock);
        var cancel = TestContext.Current.CancellationToken;
        List<double> waits = [];

        for (var i = 0; i < 6; i++)
        {
            var pending = backend.WaitBeforeLookingAgain(false, cancel);
            var due = await clock.Due();
            waits.Add(due.TotalSeconds);
            clock.Advance(due - Tick);
            Assert.False(pending.IsCompleted);
            clock.Advance(Tick);
            await pending;
        }

        Assert.Equal(new double[] { 3, 6, 12, 24, 30, 30 }, waits);
    }

    /// <summary>A dropped stream is tried again after 250 ms, and the next search starts from 3 s rather than the wait it had reached.</summary>
    [Fact]
    public async Task A_lost_stream_waits_250_ms_and_the_search_starts_from_3_seconds()
    {
        var clock = new Clock();
        var backend = new WinlandBackend(clock);
        var cancel = TestContext.Current.CancellationToken;

        await Finish(backend.WaitBeforeLookingAgain(false, cancel), clock);
        await Finish(backend.WaitBeforeLookingAgain(false, cancel), clock);

        var lost = backend.WaitBeforeLookingAgain(true, cancel);
        Assert.Equal(TimeSpan.FromMilliseconds(250), await clock.Due());
        clock.Advance(TimeSpan.FromMilliseconds(250) - Tick);
        Assert.False(lost.IsCompleted);
        clock.Advance(Tick);
        await lost;

        var again = backend.WaitBeforeLookingAgain(false, cancel);
        Assert.Equal(TimeSpan.FromSeconds(3), await clock.Due());
        clock.Advance(TimeSpan.FromSeconds(3));
        await again;
    }

    private static async Task Finish(Task pending, Clock clock)
    {
        var due = await clock.Due();
        clock.Advance(due);
        await pending;
    }

    /// <summary>A fake clock that tells when the backoff waits, and for how long.</summary>
    private sealed class Clock() : FakeTimeProvider(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero))
    {
        private readonly SemaphoreSlim _waits = new(0);
        private TimeSpan _due;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _due = dueTime;
            var timer = base.CreateTimer(callback, state, dueTime, period);
            _waits.Release();
            return timer;
        }

        public async Task<TimeSpan> Due()
        {
            await _waits.WaitAsync(TestContext.Current.CancellationToken);
            return _due;
        }
    }
}
