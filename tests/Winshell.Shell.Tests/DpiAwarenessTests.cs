using Winshell.Native;

namespace Winshell.ShellTests;

[TestClass]
public sealed class DpiAwarenessTests
{
    [TestMethod]
    public void The_process_is_per_monitor_v2_aware()
    {
        Assert.IsTrue(ThreadDpi.IsPerMonitorV2(), "This process is not per-monitor v2 DPI aware.");
    }
}
