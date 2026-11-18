namespace Winshell.TestSupport;

public static class CurrentTest
{
#pragma warning disable MSTESTEXP
    public static TestContext? Context => TestContext.Current;
#pragma warning restore MSTESTEXP

    public static CancellationToken Cancellation => Context?.CancellationToken ?? CancellationToken.None;
}
