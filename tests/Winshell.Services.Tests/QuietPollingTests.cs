using Winshell.Winland;

namespace Winshell.Services.Tests;

public sealed class QuietPollingTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    [Fact]
    public async Task Looking_for_winland_waits_250_ms_between_tries()
    {
        var clock = new Clock();
        var backend = new WinlandBackend(clock);
        var cancel = TestContext.Current.CancellationToken;

        for (var i = 0; i < 3; i++)
        {
            var pending = backend.WaitBeforeLookingAgain(cancel);
            var due = await clock.Due();
            Assert.Equal(TimeSpan.FromMilliseconds(250), due);
            clock.Advance(due - Tick);
            Assert.False(pending.IsCompleted);
            clock.Advance(Tick);
            await pending;
        }
    }

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
