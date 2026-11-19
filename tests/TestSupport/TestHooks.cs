using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

[assembly: Parallelize(Scope = ExecutionScope.ClassLevel)]

namespace Winshell.TestSupport;

[TestClass]
public static class TestHooks
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(2);
    private static readonly ConcurrentDictionary<TestContext, Timer> Deadlines = new();

    [AssemblyInitialize]
    public static void Start(TestContext context)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }

    [GlobalTestInitialize]
    public static void Before(TestContext context)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        var timeout = typeof(TestHooks).Assembly.GetType(context.FullyQualifiedTestClassName!)
            ?.GetMethod(context.TestName!)?.GetCustomAttribute<TimeoutAttribute>()?.Timeout ?? 0;
        var limit = timeout > Limit.TotalMilliseconds ? TimeSpan.FromMilliseconds(timeout) : Limit;
        var source = context.CancellationTokenSource;
        Deadlines[context] = new Timer(_ => source.Cancel(), null, limit, Timeout.InfiniteTimeSpan);
    }

    [GlobalTestCleanup]
    public static void After(TestContext context)
    {
        if (Deadlines.TryRemove(context, out var deadline))
            deadline.Dispose();
    }
}
