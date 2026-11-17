using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Winshell.Native.ProcessImage;

namespace Winshell.TestSupport;

internal static class ParentProcessWatch
{
    [ModuleInitializer]
    internal static void Start()
    {
        var parent = Parent();
        if (parent is null)
            return;
        new Thread(() =>
            {
                parent.WaitForExit();
                Process.GetCurrentProcess().Kill();
            })
        { IsBackground = true, Name = "Parent process watch" }.Start();
    }

    private static Process? Parent()
    {
        var id = ParentOf((uint)Environment.ProcessId) ??
                 throw new InvalidOperationException("No process snapshot names this process's parent.");
        try
        {
            return Process.GetProcessById((int)id);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
