using System.Collections.Concurrent;
using System.Reflection;
using Xunit.v3;

[assembly: TestDeadline]

namespace Winshell.TestSupport;

[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class TestDeadlineAttribute : BeforeAfterTestAttribute
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(2);
    private static readonly ConcurrentDictionary<string, Timer> Deadlines = new();

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        var context = TestContext.Current;
        var timeout = methodUnderTest.GetCustomAttribute<FactAttribute>()?.Timeout ?? 0;
        var limit = timeout > Limit.TotalMilliseconds ? TimeSpan.FromMilliseconds(timeout) : Limit;
        Deadlines[test.UniqueID] = new Timer(_ => context.CancelCurrentTest(), null, limit, Timeout.InfiniteTimeSpan);
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (Deadlines.TryRemove(test.UniqueID, out var deadline))
            deadline.Dispose();
    }
}
