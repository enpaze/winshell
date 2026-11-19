using System.Collections.ObjectModel;
using Windows.Foundation;
using Winshell.Power;

namespace Winshell.Services.Tests;

[TestClass]
public class BrightnessRefreshTests
{
    [TestMethod]
    public void A_failed_read_keeps_the_last_reading_of_a_monitor()
    {
        var last = new BrightnessInfo(@"\.\DISPLAY2", "Desk", BrightnessConnection.External, 0, 100, 40);

        var kept = BrightnessBackend.External(@"\.\DISPLAY2", "Desk", null, [last]);

        Assert.AreEqual(last, kept);
    }

    [TestMethod]
    public void A_failed_read_of_a_monitor_never_read_gives_nothing_and_a_good_read_replaces_the_old_one()
    {
        var last = new BrightnessInfo(@"\.\DISPLAY2", "Desk", BrightnessConnection.External, 0, 100, 40);

        Assert.IsNull(BrightnessBackend.External(@"\.\DISPLAY3", "Other", null, [last]));
        Assert.AreEqual(
            new BrightnessInfo(@"\.\DISPLAY2", "Desk", BrightnessConnection.External, 0, 100, 70),
            BrightnessBackend.External(@"\.\DISPLAY2", "Desk", (0, 70, 100), [last]));
    }

    [TestMethod]
    public void A_moved_work_area_reads_nothing_and_a_mode_change_or_a_new_monitor_reads_again()
    {
        var loop = new ManualEventLoop();
        var screen = new ScreenInfo(@"\.\DISPLAY1", "DISPLAY1", "", "", "", new Rect(0, 0, 1920, 1080),
            new Rect(0, 0, 1920, 1080), 1, 0, 0, 60, true);
        var screens = new ObservableCollection<ScreenInfo> { screen };
        var source = new FakeBrightnessBackend(
            new BrightnessSnapshot(
                [new BrightnessInfo("d", "Monitor", BrightnessConnection.External, 0, 100, 10)]),
            (_, _) => null);
        var brightness = new BrightnessService(loop, Log.Default, screens, source);
        using var stopBrightness = brightness.Stopping();
        loop.Process();
        Assert.AreEqual(1, source.Reads);

        screens[0] = screen with { WorkArea = new Rect(0, 40, 1920, 1040) };
        loop.Process();
        Assert.AreEqual(1, source.Reads);

        screens[0] = screens[0] with { RefreshRate = 144 };
        loop.Process();
        Assert.AreEqual(2, source.Reads);

        screens.Add(new ScreenInfo(@"\.\DISPLAY2", "DISPLAY2", "", "", "", default, default, 1, 0, 0, 60, false));
        loop.Process();
        Assert.AreEqual(3, source.Reads);
    }

    [TestMethod]
    public void A_new_screen_field_has_to_say_whether_it_re_reads_brightness()
    {
        Assert.AreSequenceEqual<string>(
            ["DevicePixelRatio", "Geometry", "Id", "IsPrimary", "Manufacturer", "Model", "Name",
                "PhysicalHeightMm", "PhysicalWidthMm", "RefreshRate", "SerialNumber", "WorkArea"],
            typeof(ScreenInfo).GetProperties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
    }

    [TestMethod]
    public void A_burst_of_writes_asks_for_one_read_after_the_queue_empties()
    {
        var drained = 0;
        var writes = 0;
        using var started = new SemaphoreSlim(0);
        using var release = new ManualResetEventSlim();
        var writer = new BrightnessWriter(
            (_, _) =>
            {
                Interlocked.Increment(ref writes);
                started.Release();
                release.Wait(TimeSpan.FromSeconds(10), CurrentTest.Cancellation);
                return null;
            },
            Log.Default,
            () => Interlocked.Increment(ref drained));

        writer.Request("DISPLAY1", 10, _ => { });
        try
        {
            started.Wait(CurrentTest.Cancellation);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("The first write did not start.");
        }
        for (var value = 11; value <= 30; value++)
            writer.Request("DISPLAY1", value, _ => { });
        Assert.AreEqual(0, Volatile.Read(ref drained));

        release.Set();
        Wait.Until(() => Volatile.Read(ref drained) == 1, "The queue did not empty.");

        Assert.AreEqual(2, Volatile.Read(ref writes));
        Assert.AreEqual(1, Volatile.Read(ref drained));
    }
}
