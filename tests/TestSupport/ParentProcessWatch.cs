using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Winshell.Native.ProcessImage;

namespace Winshell.TestSupport;

/// <summary>
/// Ends a test program when the process that started it is gone. xUnit v3 runs tests in a program of its own under
/// testhost. A hang timeout that names no dump type kills only testhost, and the hung program then holds the pipe
/// that <c>dotnet test</c> writes to, so a run piped to anything never ends.
/// </summary>
public static class ParentProcessWatch
{
    // The watch starts when a test program loads this assembly. The attribute belongs on the library: each test
    // program only forces the load.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
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
