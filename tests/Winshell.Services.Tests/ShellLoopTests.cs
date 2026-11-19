using Winshell.Audio;
using Winshell.Capture;
using Winshell.Notifications;

namespace Winshell.Services.Tests;

public sealed class ShellLoopTests
{
    [Fact]
    public void A_toast_a_peak_monitor_or_a_capture_session_made_before_the_shell_starts_says_to_call_Shell_Run()
    {
        Func<object>[] made = [() => new ToastMessage(), () => new AudioPeakMonitor(), () => new CaptureSession(new Log())];

        foreach (var make in made)
            Assert.Equal("The shell has not started. Call Shell.Run first.",
                Assert.Throws<InvalidOperationException>(make).Message);
    }
}
