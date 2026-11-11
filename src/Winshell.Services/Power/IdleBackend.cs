using Winshell.Native;

namespace Winshell.Power;

/// <summary>How long the person has left the PC alone, on Windows' input clock.</summary>
internal interface IIdleBackend
{
    /// <summary>
    /// Seconds since the person last used the PC, not counting input this process sent. 0 when Windows won't say.
    /// </summary>
    double SecondsSinceLastUse();
}

/// <summary>The idle time <see cref="SentInput.SecondsSinceLastUse"/> reports.</summary>
internal sealed class IdleBackend : IIdleBackend
{
    public double SecondsSinceLastUse()
    {
        return SentInput.SecondsSinceLastUse() ?? 0;
    }
}
