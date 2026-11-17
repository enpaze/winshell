using System.Reflection;

namespace Winshell.Tests;

public class EntryPointTests
{
    [Fact]
    public void The_main_thread_is_a_single_threaded_apartment()
    {
        var main = typeof(Cli).Assembly.EntryPoint;
        Assert.NotNull(main);
        Assert.NotNull(main.GetCustomAttribute<STAThreadAttribute>());
    }
}
