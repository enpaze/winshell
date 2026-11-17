using System.ComponentModel;
using System.Diagnostics;
using Winshell.Native;

namespace Winshell.Apps;

internal static class AppIdentity
{
    public static bool IsPackaged(string appId)
    {
        return appId.Contains('!');
    }

    public static string DesktopId(string path)
    {
        return path.Length == 0 ? "" : Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
    }

    public static string IconPath(string appId, string program)
    {
        return IsPackaged(appId) ? AppsFolderPath(appId) : program;
    }

    public static string AppsFolderPath(string appId)
    {
        return @"shell:AppsFolder\" + appId;
    }

    public static string? OpenAppsFolder(string appId)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe", AppsFolderPath(appId))
            { UseShellExecute = true });
            return null;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            return e.Message;
        }
    }

    public static string? ProgramName(string appId)
    {
        return !IsPackaged(appId) && appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? appId[..^4]
            : null;
    }

    public static string FallbackName(string appId)
    {
        return ProgramName(appId) ?? appId;
    }

    public static string ExpandKnownFolder(string path)
    {
        if (path.Length < 3 || path[0] != '{')
            return path;
        var end = path.IndexOf('}');
        if (end <= 0 || !Guid.TryParse(path[..(end + 1)], out var id))
            return path;
        if (KnownFolder.Find(id) is not { Length: > 0 } folder)
            return path;
        var rest = path[(end + 1)..].TrimStart('\\', '/');
        return rest.Length == 0 ? folder : Path.Combine(folder, rest);
    }
}
