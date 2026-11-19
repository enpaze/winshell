using Winshell.Audio;
using Winshell.Capture;
using Winshell.Notifications;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class ShellLoopTests
{
    [TestMethod]
    public void A_toast_a_peak_monitor_or_a_capture_session_made_before_the_shell_starts_says_to_call_Shell_Run()
    {
        Func<object>[] made = [() => new ToastMessage(), () => new AudioPeakMonitor(), () => new CaptureSession(new Log())];

        foreach (var make in made)
            Assert.AreEqual("The shell has not started. Call Shell.Run first.",
                Assert.ThrowsExactly<InvalidOperationException>(make).Message);
    }

    [TestMethod]
    public void A_toast_made_while_the_shell_runs_reports_its_failure_on_the_shell_loop()
    {
        var loop = new ManualEventLoop();
        using var running = Running(loop);
        var toast = new ToastMessage();
        var failures = new List<string>();
        toast.Failed += failures.Add;

        Assert.IsFalse(toast.Send());
        Assert.IsEmpty(failures);

        loop.Process();

        Assert.AreSequenceEqual(["toast.appId must be an installed Windows AppUserModelID."], failures);
        Assert.AreEqual(failures[0], toast.Error);
    }

    [TestMethod]
    public void A_peak_monitor_made_while_the_shell_runs_reads_on_the_shell_loop()
    {
        var loop = new ManualEventLoop();
        var graph = new FakeAudioBackend();
        var meter = new FakeMeter(0.25f);
        graph.Add(new FakeGraphNode(AudioNodeType.RecordingDevice) { FakeMeter = meter });
        var audio = new AudioService(loop, new Log(), graph);
        using var stopAudio = audio.Stopping();
        using var running = Running(loop);
        using var monitor = new AudioPeakMonitor();
        monitor.Audio = audio.RecordingDevices[0];
        Assert.AreEqual(0.25, monitor.Peak);

        meter.Peaks = [0.75f];
        loop.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(0.75, monitor.Peak);
    }

    private static DisposeAction Running(IEventLoop loop)
    {
        EventLoop.Current = loop;
        return new DisposeAction(() => EventLoop.Current = null!);
    }
}
