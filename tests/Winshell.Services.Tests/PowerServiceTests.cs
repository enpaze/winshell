using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Winshell.Power;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class PowerServiceTests
{
    private static PowerDeviceInfo Battery(string id, double percentage = 0.5)
    {
        return new PowerDeviceInfo(id, "Model", PowerDeviceType.Battery, true, true,
            PowerDeviceState.Discharging, percentage, 10, 20, -5, 3600, 0, 90, true);
    }

    private static PowerSnapshot Snapshot(params PowerDeviceInfo[] devices)
    {
        return new PowerSnapshot(devices.FirstOrDefault() ?? PowerDeviceInfo.NoBattery, devices,
            devices.Length > 0);
    }

    private static BrightnessInfo Monitor(int current)
    {
        return new BrightnessInfo("d", "Monitor", BrightnessConnection.External, 0, 100, current);
    }

    private static List<string> Watch(INotifyPropertyChanged source)
    {
        var changes = new List<string>();
        source.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        return changes;
    }

    [TestMethod]
    public void A_snapshot_changes_only_the_properties_that_differ()
    {
        var loop = new ManualEventLoop();
        var source = new FakePowerBackend(Snapshot(Battery("a")));
        var power = new PowerService(loop, Log.Default, source);
        using var stopPower = power.Stopping();
        loop.Process();
        var changes = Watch(power.DisplayDevice);

        source.Push(Snapshot(Battery("a", 0.25)));
        Assert.IsEmpty(changes);
        loop.Process();

        Assert.AreSequenceEqual([nameof(PowerDevice.Percentage)], changes);
        Assert.AreEqual(0.25, power.DisplayDevice.Percentage);
    }

    [TestMethod]
    public void A_device_that_stays_is_the_same_object()
    {
        var loop = new ManualEventLoop();
        var source = new FakePowerBackend(Snapshot(Battery("a"), Battery("b")));
        var power = new PowerService(loop, Log.Default, source);
        using var stopPower = power.Stopping();
        loop.Process();
        var kept = power.Devices[1];

        source.Push(Snapshot(Battery("b", 0.75)));
        loop.Process();

        var only = Assert.ContainsSingle(power.Devices);
        Assert.AreSame(kept, only);
        Assert.AreEqual(0.75, only.Percentage);
    }

    [TestMethod]
    public void A_disposed_service_stops_following_the_source()
    {
        var loop = new ManualEventLoop();
        var source = new FakePowerBackend(Snapshot());
        var power = new PowerService(loop, Log.Default, source);

        source.Push(Snapshot(Battery("a")));
        power.Stop();
        loop.Process();

        Assert.IsEmpty(power.Devices);
        Assert.IsTrue(source.Disposed);
    }

    [TestMethod]
    public void A_battery_at_zero_health_with_support_reads_as_supported()
    {
        Assert.AreEqual((0d, true), PowerBackend.Health(0, 1000));
        Assert.AreEqual((50d, true), PowerBackend.Health(500, 1000));
        Assert.AreEqual((100d, true), PowerBackend.Health(2000, 1000));
        Assert.AreEqual((0d, false), PowerBackend.Health(null, 1000));
        Assert.AreEqual((0d, false), PowerBackend.Health(500, null));
        Assert.AreEqual((0d, false), PowerBackend.Health(500, 0));
        Assert.AreEqual((0d, false), PowerBackend.Health(-1, 1000));

        var loop = new ManualEventLoop();
        var dead = Battery("a") with { HealthPercentage = 0, HealthSupported = true };
        var source = new FakePowerBackend(Snapshot(dead));
        var power = new PowerService(loop, Log.Default, source);
        using var stopPower = power.Stopping();
        loop.Process();

        Assert.AreEqual(0, power.DisplayDevice.HealthPercentage);
        Assert.IsTrue(power.DisplayDevice.HealthSupported);
    }

    [TestMethod]
    public void An_unanswered_power_mode_reads_as_unknown()
    {
        var loop = new ManualEventLoop();
        var asked = 0;
        var mode = new PowerModeService(loop, Log.Default, new FakePowerModeBackend(_ =>
        {
            asked++;
            return 0;
        }));
        using var stopMode = mode.Stopping();

        Assert.AreEqual(PowerModeLevel.Unknown, mode.Mode);
        Assert.AreEqual(EffectivePowerMode.Unknown, mode.Effective);

        loop.Process();

        Assert.AreEqual(PowerModeLevel.Unknown, mode.Mode);
        Assert.AreEqual(EffectivePowerMode.Unknown, mode.Effective);
        Assert.IsFalse(mode.Available);

        mode.Mode = PowerModeLevel.Unknown;
        Assert.AreEqual(0, asked);
    }

    [TestMethod]
    public void A_power_mode_Windows_refuses_is_logged()
    {
        var lines = new List<string>();
        var log = new Log { Sink = (_, _, message) => lines.Add(message) };
        var mode = new PowerModeService(new ManualEventLoop(), log, new FakePowerModeBackend(_ => 5));
        using var stopMode = mode.Stopping();

        mode.Mode = PowerModeLevel.BestPerformance;

        Assert.Contains(l => l.Contains("error 5", StringComparison.Ordinal), lines);
    }

    [TestMethod]
    public void A_power_mode_that_is_not_a_level_is_refused()
    {
        var lines = new List<string>();
        var log = new Log { Sink = (_, _, message) => lines.Add(message) };
        var asked = 0;
        var mode = new PowerModeService(new ManualEventLoop(), log, new FakePowerModeBackend(_ =>
        {
            asked++;
            return 0;
        }));
        using var stopMode = mode.Stopping();

        mode.Mode = (PowerModeLevel)9;

        Assert.AreEqual(0, asked);
        Assert.ContainsSingle(lines);
    }

    [TestMethod]
    public async Task A_brightness_shows_at_once_and_a_later_read_replaces_it()
    {
        var loop = new ManualEventLoop();
        var written = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new FakeBrightnessBackend(new BrightnessSnapshot([Monitor(10)]), (_, value) =>
        {
            written.TrySetResult(value);
            return null;
        });
        var brightness = new BrightnessService(loop, Log.Default, new ObservableCollection<ScreenInfo>(), source);
        using var stopBrightness = brightness.Stopping();
        loop.Process();
        var monitor = brightness.Monitors[0];

        monitor.Brightness = 150;

        Assert.AreEqual(100, monitor.Brightness);
        Assert.AreEqual(100, await written.Task.WaitAsync(CurrentTest.Cancellation));
        await Wait.UntilAsync(() => source.Reads == 2, "the write to end");
        source.Push(new BrightnessSnapshot([Monitor(60)]));
        loop.Process();
        Assert.AreEqual(60, monitor.Brightness);
    }

    [TestMethod]
    public void An_idle_monitor_turns_on_after_the_timeout_and_a_keep_awake_holds_it_off()
    {
        var loop = new ManualEventLoop();
        var idle = new StrongBox<double>(0);
        var held = new StrongBox<bool>(false);
        using var monitor = new IdleMonitor(loop, Log.Default, () => held.Value, new IdleSeconds(() => idle.Value));
        monitor.Timeout = 10;
        Assert.IsFalse(monitor.IsIdle);

        idle.Value = 11;
        held.Value = true;
        loop.Advance(TimeSpan.FromSeconds(5));
        Assert.IsFalse(monitor.IsIdle);

        held.Value = false;
        loop.Advance(TimeSpan.FromSeconds(5));
        Assert.IsTrue(monitor.IsIdle);
    }

    [TestMethod]
    public void An_idle_monitor_that_cannot_be_idle_does_not_poll()
    {
        var loop = new ManualEventLoop();
        using var monitor = new IdleMonitor(loop, Log.Default, () => false, new IdleSeconds(() => 0));
        monitor.Timeout = 10;

        monitor.Enabled = false;

        Assert.IsNull(loop.NextDue);
    }

    [TestMethod]
    [DataRow(false, 100, 5)]
    [DataRow(false, 2, 2)]
    [DataRow(false, 0.1, 0.25)]
    [DataRow(true, 100, 1)]
    [DataRow(false, -1, 1)]
    public void An_idle_monitor_looks_again_when_the_timeout_can_pass(bool idle, double remaining, double seconds)
    {
        Assert.AreEqual(TimeSpan.FromSeconds(seconds), IdleMonitor.NextCheck(idle, remaining));
    }

    private sealed class IdleSeconds(Func<double> seconds) : IIdleBackend
    {
        public double SecondsSinceLastUse()
        {
            return seconds();
        }
    }
}
