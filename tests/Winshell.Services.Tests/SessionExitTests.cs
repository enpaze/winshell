using System.Diagnostics;
using System.Runtime.CompilerServices;
using Winshell.Audio;

namespace Winshell.Services.Tests;

[TestClass]
public class SessionExitTests
{
    [TestMethod]
    public void Stopping_the_watch_drops_the_exit_callback()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -n 60 127.0.0.1 >nul",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        Assert.IsNotNull(process);
        var ran = new StrongBox<int>(0);
        EventHandler handler = (_, _) => Interlocked.Increment(ref ran.Value);
        process.EnableRaisingEvents = true;
        process.Exited += handler;

        SessionNode.StopWatching(process, handler);
        try
        {
            process.Kill(true);
        }
        catch (InvalidOperationException)
        {
        }

        Assert.IsTrue(process.WaitForExit(8000));
        Wait.ForNothingToHappen(TimeSpan.FromMilliseconds(200));
        Assert.AreEqual(0, Volatile.Read(ref ran.Value));
    }
}
