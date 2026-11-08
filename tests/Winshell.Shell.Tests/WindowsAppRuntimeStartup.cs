using System.Runtime.CompilerServices;
using Microsoft.Windows.Foundation.UndockedRegFreeWinRTCS;

namespace Winshell.ShellTests;

/// <summary>
/// Loads the self-contained Windows App Runtime before a test uses WinUI. xUnit's discovery probe is the same
/// program started with <c>-assemblyInfo</c>, and it only has to print assembly info and exit. Loading the runtime
/// there is what makes that process miss xUnit's 60 second limit.
/// </summary>
internal static class WindowsAppRuntimeStartup
{
    [ModuleInitializer]
    internal static void LoadUnlessDiscoveryProbe()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 1; i < args.Length; i++)
            if (args[i] == "-assemblyInfo")
                return;

        AutoInitialize.AccessWindowsAppSDK();
    }
}
