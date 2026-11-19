namespace Winshell.Tests;

[TestClass]
public sealed partial class ShellSupervisorTests
{
    private const int AccessViolation = -1073741819;
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    private readonly Clock _time = new();
    private readonly Shells _shells = new();

    [TestMethod]
    public async Task A_crash_brings_the_taskbar_back_before_the_wait_and_starts_the_shell_again_after_it()
    {
        var run = Supervise(CancellationToken.None);
        var first = await _shells.Started(1);

        first.Exit(AccessViolation);
        await _time.Waiting();
        Assert.AreSequenceEqual(["start", "restore"], _shells.Events);
        _time.Advance(TimeSpan.FromSeconds(1) - Tick);
        Assert.AreSequenceEqual(["start", "restore"], _shells.Events);
        _time.Advance(Tick);
        var second = await _shells.Started(2);

        second.Exit(0);
        Assert.AreEqual(0, await run);
        Assert.AreSequenceEqual(["start", "restore", "start"], _shells.Events);
    }

    [TestMethod]
    public async Task Each_crash_in_a_row_waits_twice_as_long_up_to_ten_seconds_and_the_sixth_ends_supervision()
    {
        var run = Supervise(CancellationToken.None);
        int[] waits = [1, 2, 4, 8, 10];

        for (var i = 0; i < waits.Length; i++)
        {
            (await _shells.Started(i + 1)).Exit(AccessViolation);
            await _time.Waiting();
            _time.Advance(TimeSpan.FromSeconds(waits[i]) - Tick);
            Assert.AreEqual(i + 1, _shells.Count);
            _time.Advance(Tick);
        }

        (await _shells.Started(6)).Exit(AccessViolation);

        Assert.AreEqual(AccessViolation, await run);
        Assert.AreSequenceEqual(Enumerable.Repeat<string[]>(["start", "restore"], 6).SelectMany(e => e), _shells.Events);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual(6, _shells.Count);
    }

    [TestMethod]
    public async Task A_crash_after_a_minute_of_running_waits_as_long_as_the_first()
    {
        var run = Supervise(CancellationToken.None);
        (await _shells.Started(1)).Exit(AccessViolation);
        await _time.Waiting();
        _time.Advance(TimeSpan.FromSeconds(1));
        (await _shells.Started(2)).Exit(AccessViolation);
        await _time.Waiting();
        _time.Advance(TimeSpan.FromSeconds(2));
        var third = await _shells.Started(3);

        _time.Advance(TimeSpan.FromMinutes(1));
        third.Exit(AccessViolation);
        await _time.Waiting();
        _time.Advance(TimeSpan.FromSeconds(1) - Tick);
        Assert.AreEqual(3, _shells.Count);
        _time.Advance(Tick);
        var fourth = await _shells.Started(4);

        fourth.Exit(0);
        Assert.AreEqual(0, await run);
    }

    [TestMethod]
    public async Task A_clean_exit_ends_supervision_and_leaves_the_taskbar_to_the_shell()
    {
        var run = Supervise(CancellationToken.None);

        (await _shells.Started(1)).Exit(0);

        Assert.AreEqual(0, await run);
        Assert.AreSequenceEqual(["start"], _shells.Events);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual(1, _shells.Count);
    }

    [TestMethod]
    public async Task Exit_code_1_ends_supervision_with_the_taskbar_back()
    {
        var run = Supervise(CancellationToken.None);

        (await _shells.Started(1)).Exit(1);

        Assert.AreEqual(1, await run);
        Assert.AreSequenceEqual(["start", "restore"], _shells.Events);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual(1, _shells.Count);
    }

    [TestMethod]
    public async Task A_crash_after_a_stop_was_asked_for_ends_supervision_with_the_taskbar_back()
    {
        var run = Supervise(CancellationToken.None);

        (await _shells.Started(1)).Exit(-1, ExitIntent.Stop);

        Assert.AreEqual(-1, await run);
        Assert.AreSequenceEqual(["start", "restore"], _shells.Events);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual(1, _shells.Count);
    }

    [TestMethod]
    [DataRow(0, new[] { "start", "start" })]
    [DataRow(AccessViolation, new[] { "start", "restore", "start" })]
    public async Task A_reload_starts_the_shell_again_at_once(int code, string[] events)
    {
        var run = Supervise(CancellationToken.None);

        (await _shells.Started(1)).Exit(code, ExitIntent.Reload);
        var second = await _shells.Started(2);

        Assert.AreSequenceEqual(events, _shells.Events);
        second.Exit(0);
        Assert.AreEqual(0, await run);
    }

    [TestMethod]
    public async Task A_shell_a_reload_started_waits_as_long_as_the_first_after_its_crash()
    {
        var run = Supervise(CancellationToken.None);
        (await _shells.Started(1)).Exit(AccessViolation);
        await _time.Waiting();
        _time.Advance(TimeSpan.FromSeconds(1));
        (await _shells.Started(2)).Exit(AccessViolation);
        await _time.Waiting();
        _time.Advance(TimeSpan.FromSeconds(2));
        (await _shells.Started(3)).Exit(0, ExitIntent.Reload);

        (await _shells.Started(4)).Exit(AccessViolation);
        await _time.Waiting();
        _time.Advance(TimeSpan.FromSeconds(1) - Tick);
        Assert.AreEqual(4, _shells.Count);
        _time.Advance(Tick);
        var fifth = await _shells.Started(5);

        fifth.Exit(0);
        Assert.AreEqual(0, await run);
    }

    [TestMethod]
    public async Task A_crash_of_a_shell_a_reload_started_brings_the_taskbar_back()
    {
        var run = Supervise(CancellationToken.None);
        (await _shells.Started(1)).Exit(0, ExitIntent.Reload);

        (await _shells.Started(2)).Exit(AccessViolation);
        await _time.Waiting();

        Assert.AreSequenceEqual(["start", "start", "restore"], _shells.Events);
        _time.Advance(TimeSpan.FromSeconds(1));
        (await _shells.Started(3)).Exit(0);
        Assert.AreEqual(0, await run);
    }

    [TestMethod]
    public async Task A_shell_that_does_not_start_ends_supervision_with_the_taskbar_back()
    {
        _shells.FailToStart = true;

        Assert.AreEqual(1, await Supervise(CancellationToken.None));

        Assert.AreSequenceEqual(["start", "restore"], _shells.Events);
    }

    [TestMethod]
    public async Task A_stop_stops_the_running_shell_without_a_new_start()
    {
        using var stop = new CancellationTokenSource();
        var run = Supervise(stop.Token);
        await _shells.Started(1);

        await stop.CancelAsync();

        Assert.AreEqual(0, await run);
        Assert.AreSequenceEqual(["start", "stop"], _shells.Events);
    }

    [TestMethod]
    public async Task A_stop_during_the_wait_ends_supervision_without_a_new_start()
    {
        using var stop = new CancellationTokenSource();
        var run = Supervise(stop.Token);
        (await _shells.Started(1)).Exit(AccessViolation);
        await _time.Waiting();

        await stop.CancelAsync();

        Assert.AreEqual(AccessViolation, await run);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.AreSequenceEqual(["start", "restore"], _shells.Events);
    }

    [TestMethod]
    public async Task A_taskbar_that_was_hidden_when_the_shell_started_stays_hidden()
    {
        _shells.TaskbarHidden = true;
        var run = Supervise(CancellationToken.None);

        (await _shells.Started(1)).Exit(1);

        Assert.AreEqual(1, await run);
        Assert.AreSequenceEqual(["start"], _shells.Events);
    }

    private Task<int> Supervise(CancellationToken stop)
    {
        var supervisor = new ShellSupervisor(_shells.Start, () => _shells.TaskbarHidden, (_, hidden) => hidden,
            _shells.Restore, _time);
        return Task.Run(() => supervisor.RunAsync(stop), CurrentTest.Cancellation);
    }

    private sealed class Clock() : FakeTimeProvider(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero))
    {
        private readonly SemaphoreSlim _waits = new(0);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            _waits.Release();
            return timer;
        }

        public Task Waiting()
        {
            return _waits.WaitAsync(CurrentTest.Cancellation);
        }
    }

    private sealed class Shells
    {
        private readonly Lock _gate = new();
        private readonly List<string> _events = [];
        private readonly List<Child> _children = [];
        private readonly SemaphoreSlim _started = new(0);

        public bool TaskbarHidden { get; set; }

        public bool FailToStart { get; set; }

        public string[] Events
        {
            get
            {
                lock (_gate)
                {
                    return [.. _events];
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _children.Count;
                }
            }
        }

        public IShellChild? Start()
        {
            Add("start");
            if (FailToStart)
                return null;
            var child = new Child(this);
            lock (_gate)
            {
                _children.Add(child);
            }

            _started.Release();
            return child;
        }

        public void Restore()
        {
            Add("restore");
        }

        public async Task<Child> Started(int number)
        {
            while (Count < number)
                await _started.WaitAsync(CurrentTest.Cancellation);
            lock (_gate)
            {
                return _children[number - 1];
            }
        }

        public void Add(string what)
        {
            lock (_gate)
            {
                _events.Add(what);
            }
        }
    }

    private sealed partial class Child(Shells shells) : IShellChild
    {
        private readonly TaskCompletionSource<int> _exit = new();

        public Task<int> Exited => _exit.Task;

        public int Pid => 0;

        public ExitIntent Intent { get; private set; }

        public void Exit(int code, ExitIntent intent = ExitIntent.None)
        {
            Intent = intent;
            _exit.SetResult(code);
        }

        public Task StopAsync()
        {
            shells.Add("stop");
            _exit.SetResult(0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
