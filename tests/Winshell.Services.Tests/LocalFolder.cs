using System.Runtime.CompilerServices;

namespace Winshell.Services.Tests;

internal static class LocalFolder
{
    [ModuleInitializer]
    internal static void Set()
    {
        Environment.SetEnvironmentVariable("WINSHELL_LOCAL_DIR",
            TempFolder.NewPath());
    }
}
