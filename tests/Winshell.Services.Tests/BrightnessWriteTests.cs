using Winshell.Power;

namespace Winshell.Services.Tests;

/// <summary>
/// Setting a brightness does not wait for the slow write, and a drag writes only the newest value.
/// A fake write stands in for Windows, so the PC's brightness never changes.
/// </summary>
public class BrightnessWriteTests
{
    [Fact]
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

            // The writer thread signals started before either handle is disposed.
            started.Release();
            // The writer thread blocks on release and finishes before it is disposed.
            release.Wait(TestContext.Current.CancellationToken);
            return null;
        }, Log.Default);
        var monitor = new BrightnessMonitor(writer, Log.Default);
        monitor.Update(new BrightnessInfo("DISPLAY1", "Panel", BrightnessConnection.Internal, 0, 100, 40));

        // The write is held until release is set below, so a setter that waited for it would never return.
        monitor.Brightness = 50;
        Assert.Equal(50, monitor.Brightness);

        started.Wait(TestContext.Current.CancellationToken);
        for (var value = 51; value <= 80; value++)
            monitor.Brightness = value;
        Assert.Equal(80, monitor.Brightness);

        // A read that arrives while writes are still running does not move the value back.
        monitor.Update(new BrightnessInfo("DISPLAY1", "Panel", BrightnessConnection.Internal, 0, 100, 40));
        Assert.Equal(80, monitor.Brightness);

        release.Set();
        Wait.Until(() => !writer.IsWriting("DISPLAY1"), "The writes did not finish.");
        lock (written)
        {
            Assert.Equal([50, 80], written);
        }

        // After the writes, the next read is what the property shows.
        monitor.Update(new BrightnessInfo("DISPLAY1", "Panel", BrightnessConnection.Internal, 0, 100, 78));
        Assert.Equal(78, monitor.Brightness);
    }

    [Fact]
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
                // The writer thread signals started before either handle is disposed.
                started.Release();
                // The writer thread blocks on release and finishes before it is disposed.
                release.Wait(TestContext.Current.CancellationToken);
                throw new InvalidOperationException("the panel refused 10");
            }

            return null;
        }, log);

        writer.Request("DISPLAY1", 10, _ => { });
        Assert.True(started.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            "The first write did not start.");
        writer.Request("DISPLAY1", 80, _ => { });
        release.Set();
        Wait.Until(() => !writer.IsWriting("DISPLAY1"), "The writes did not finish.");

        lock (written)
        {
            Assert.Equal([10, 80], written);
        }

        Assert.Contains("the panel refused 10", warnings);
    }
}
