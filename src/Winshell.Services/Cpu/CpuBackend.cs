using Winshell.Native;

namespace Winshell.Cpu;

internal readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User)
{
    public static double Usage(CpuTimes before, CpuTimes after)
    {
        var total = after.Kernel - before.Kernel + (after.User - before.User);
        if (total == 0)
            return 0;
        var idle = Math.Min(after.Idle - before.Idle, total);
        return 100.0 * (total - idle) / total;
    }
}

internal interface ICpuBackend
{
    CpuTimes? Read();
}

internal sealed class CpuBackend : ICpuBackend
{
    public CpuTimes? Read()
    {
        return SystemTimes.Read(out var idle, out var kernel, out var user) ? new CpuTimes(idle, kernel, user) : null;
    }
}
