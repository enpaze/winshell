using System.Diagnostics;

namespace Winshell.Services.Tests;

public class InstanceListTests
{
    private static InstanceInfo Record(int pid, string processName, long processStart = 1)
    {
        return new InstanceInfo("id", pid, "shell", "C:\\cfg\\MyShell.exe", DateTime.Now, "pipe", "log.txt",
            processStart, ProcessName: processName);
    }

    [Fact]
    public void An_instance_id_that_now_belongs_to_a_process_this_user_cannot_open_is_not_running()
    {
        // Process id 4 is always the System process, which a user cannot open.
        Assert.False(Instances.IsRunning(Record(4, "myshell")));
    }

    [Fact]
    public void An_instance_id_that_now_belongs_to_another_program_is_not_running()
    {
        using var other = Process.GetCurrentProcess();
        Assert.False(Instances.IsRunning(Record(other.Id, other.ProcessName + "-other", Started(other))));
    }

    [Fact]
    public void An_instance_id_no_process_has_is_not_running()
    {
        // Process ids are multiples of four, so this one is never in use.
        Assert.False(Instances.IsRunning(Record(int.MaxValue - 2, "myshell")));
    }

    private static long Started(Process process)
    {
        return process.StartTime.ToUniversalTime().Ticks;
    }
}
