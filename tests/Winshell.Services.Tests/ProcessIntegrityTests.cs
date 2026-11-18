using Winshell.Native;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class ProcessIntegrityTests
{
    [TestMethod]
    public void The_running_process_reads_between_medium_and_system_and_the_idle_process_reads_as_unknown()
    {
        Assert.IsInRange(ProcessIntegrity.Medium, 0x4000u, ProcessIntegrity.TryRead((uint)Environment.ProcessId) ?? 0);
        Assert.IsNull(ProcessIntegrity.TryRead(0));
    }
}
