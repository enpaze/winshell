namespace Winshell.Tests;

[TestClass]
public abstract class ProbeBuild
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void Build(TestContext context)
    {
        ProbeShell.Ensure();
    }
}
