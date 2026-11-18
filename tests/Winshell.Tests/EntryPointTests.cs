using System.Reflection;

namespace Winshell.Tests;

[TestClass]
public class EntryPointTests
{
    [TestMethod]
    public void The_main_thread_is_a_single_threaded_apartment()
    {
        var main = typeof(Cli).Assembly.EntryPoint;
        Assert.IsNotNull(main);
        Assert.IsNotNull(main.GetCustomAttribute<STAThreadAttribute>());
    }
}
