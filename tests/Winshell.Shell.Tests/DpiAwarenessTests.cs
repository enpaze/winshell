using Winshell.Native;

namespace Winshell.ShellTests;

/// <summary>
/// A configuration is per-monitor v2 DPI aware. This project imports <c>Winshell.props</c>, the settings every
/// configuration is built with, so this process is that program.
/// </summary>
public sealed class DpiAwarenessTests
{
    [Fact]
    public void The_process_is_per_monitor_v2_aware()
    {
        Assert.True(ThreadDpi.IsPerMonitorV2(), "This process is not per-monitor v2 DPI aware.");
    }
}
