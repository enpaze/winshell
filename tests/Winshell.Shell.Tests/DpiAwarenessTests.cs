using Winshell.Native;

namespace Winshell.ShellTests;

public sealed class DpiAwarenessTests
{
    [Fact]
    public void The_process_is_per_monitor_v2_aware()
    {
        Assert.True(ThreadDpi.IsPerMonitorV2(), "This process is not per-monitor v2 DPI aware.");
    }
}
