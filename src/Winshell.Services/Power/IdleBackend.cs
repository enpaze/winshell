using Winshell.Native;

namespace Winshell.Power;

internal interface IIdleBackend
{
    double SecondsSinceLastUse();
}

internal sealed class IdleBackend : IIdleBackend
{
    public double SecondsSinceLastUse()
    {
        return SentInput.SecondsSinceLastUse() ?? 0;
    }
}
