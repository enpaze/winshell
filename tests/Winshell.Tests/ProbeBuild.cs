namespace Winshell.Tests;

[TestClass]
public abstract class ProbeBuild
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void Build(TestContext context)
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
