namespace Winshell.Tests;

public sealed class ProbeBuild
{
    public ProbeBuild()
    {
        _ = ProbeShell.Program;
    }
}

public sealed class OtherProbeBuild
{
    public OtherProbeBuild()
    {
        _ = ProbeShell.Other;
    }
}
