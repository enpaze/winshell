using Winshell.Power;

namespace Winshell.Services.Tests;

[TestClass]
public class BrightnessWriteTests
{
    [TestMethod]
    public void Setting_brightness_returns_at_once_and_writes_only_the_newest_value()
    {
        var written = new List<int>();
        using var started = new SemaphoreSlim(0);
        using var release = new ManualResetEventSlim();
        var writer = new BrightnessWriter((_, value) =>
        {
            lock (written)
            {
                written.Add(value);
            }

            started.Release();
            release.Wait(CurrentTest.Cancellation);
            return null;
        }, Log.Default);
        var monitor = new BrightnessMonitor(writer, Log.Default);
        monitor.Update(new BrightnessInfo("DISPLAY1", "Panel", BrightnessConnection.Internal, 0, 100, 40));

        monitor.Brightness = 50;
        Assert.AreEqual(50, monitor.Brightness);

        started.Wait(CurrentTest.Cancellation);
        for (var value = 51; value <= 80; value++)
            monitor.Brightness = value;
        Assert.AreEqual(80, monitor.Brightness);

        monitor.Update(new BrightnessInfo("DISPLAY1", "Panel", BrightnessConnection.Internal, 0, 100, 40));
        Assert.AreEqual(80, monitor.Brightness);

        release.Set();
        Wait.Until(() => !writer.IsWriting("DISPLAY1"), "The writes did not finish.");
        lock (written)
        {
            Assert.AreSequenceEqual([50, 80], written);
        }

        monitor.Update(new BrightnessInfo("DISPLAY1", "Panel", BrightnessConnection.Internal, 0, 100, 78));
        Assert.AreEqual(78, monitor.Brightness);
    }

    [TestMethod]
    public void A_failed_write_is_reported_and_the_newest_level_is_still_written()
    {
        var written = new List<int>();
        var warnings = new List<string>();
        var log = new Log { Sink = (_, _, message) => warnings.Add(message) };
        using var started = new SemaphoreSlim(0);
        using var release = new ManualResetEventSlim();
        var writer = new BrightnessWriter((_, value) =>
        {
            lock (written)
            {
                written.Add(value);
            }

            if (value == 10)
            {
                started.Release();
                release.Wait(CurrentTest.Cancellation);
                throw new InvalidOperationException("the panel refused 10");
            }

            return null;
        }, log);

        writer.Request("DISPLAY1", 10, _ => { });
        started.Wait(CurrentTest.Cancellation);
        writer.Request("DISPLAY1", 80, _ => { });
        release.Set();
        Wait.Until(() => !writer.IsWriting("DISPLAY1"), "The writes did not finish.");

        lock (written)
        {
            Assert.AreSequenceEqual([10, 80], written);
        }

        Assert.Contains("the panel refused 10", warnings);
    }
}
