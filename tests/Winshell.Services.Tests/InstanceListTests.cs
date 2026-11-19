using System.Diagnostics;

namespace Winshell.Services.Tests;

[TestClass]
public class InstanceListTests
{
    private static InstanceInfo Record(int pid, string processName, long processStart = 1)
    {
        return new InstanceInfo("id", pid, "shell", "C:\\cfg\\MyShell.exe", DateTime.Now, "pipe", "log.txt",
            processStart, ProcessName: processName);
    }

    [TestMethod]
    public void An_instance_id_that_now_belongs_to_a_process_this_user_cannot_open_is_not_running()
    {
        Assert.IsFalse(Instances.IsRunning(Record(4, "myshell")));
    }

    [TestMethod]
    public void An_instance_id_that_now_belongs_to_another_program_is_not_running()
    {
        using var other = Process.GetCurrentProcess();
        Assert.IsFalse(Instances.IsRunning(Record(other.Id, other.ProcessName + "-other", Started(other))));
    }

    [TestMethod]
    public void An_instance_id_no_process_has_is_not_running()
    {
        Assert.IsFalse(Instances.IsRunning(Record(int.MaxValue - 2, "myshell")));
    }

    private static long Started(Process process)
    {
        return process.StartTime.ToUniversalTime().Ticks;
    }
}
