using System.ComponentModel;
using System.Diagnostics;
using Winshell.Native;

namespace Winshell.Apps;

internal static class AppDisplayName
{
    public static string Find(string appId)
    {
        var name = appId.Length == 0 ? null : ShellItems.Name(AppIdentity.AppsFolderPath(appId));
        if (string.IsNullOrWhiteSpace(name) && AppIdentity.ProgramName(appId) is { } program)
            name = RunningProgramDescription(program);
        return string.IsNullOrWhiteSpace(name) ? AppIdentity.FallbackName(appId) : name;
    }

    private static string? RunningProgramDescription(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { } path &&
                        AppsService.ProgramDescription(path) is { } description)
                        return description;
                }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException)
                {
                }
            }

        return null;
    }
}
