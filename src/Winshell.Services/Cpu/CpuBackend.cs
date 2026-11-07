using Winshell.Native;

namespace Winshell.Cpu;

/// <summary>
/// The time the processors have spent since Windows started, in 100 ns ticks summed over every core. Kernel time
/// includes idle time.
/// </summary>
internal readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User)
{
    /// <summary>
    /// The share of the time from <paramref name="before"/> to <paramref name="after"/> the processors were busy, from 0
    /// to 100. 0 when no time passed.
    /// </summary>
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
    /// <summary>Reads the processor times, or null when Windows fails.</summary>
    CpuTimes? Read();
}

/// <summary>The processor times as Windows reports them.</summary>
internal sealed class CpuBackend : ICpuBackend
{
    public CpuTimes? Read()
    {
        return SystemTimes.Read(out var idle, out var kernel, out var user) ? new CpuTimes(idle, kernel, user) : null;
    }
}
