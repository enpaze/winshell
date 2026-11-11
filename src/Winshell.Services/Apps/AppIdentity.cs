using System.ComponentModel;
using System.Diagnostics;
using Winshell.Native;

namespace Winshell.Apps;

/// <summary>
/// One app's identity: the packaged-id rule, the desktop id, the icon path, the program description and known-folder
/// expansion. A packaged id contains <c>!</c>. A desktop id is the file name in lower case, without the extension.
/// A media id can still be the file name with <c>.exe</c>, such as <c>Spotify.exe</c>.
/// </summary>
internal static class AppIdentity
{
    /// <summary>Whether the id is a packaged (Store) app's.</summary>
    public static bool IsPackaged(string appId)
    {
        return appId.Contains('!');
    }

    /// <summary>A desktop program's app id: its file name in lower case, without the extension.</summary>
    public static string DesktopId(string path)
    {
        return path.Length == 0 ? "" : Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
    }

    /// <summary>
    /// A path an icon is read from. A packaged app's icon is the one Start shows for its id. Every other app uses its
    /// program's file.
    /// </summary>
    public static string IconPath(string appId, string program)
    {
        return IsPackaged(appId) ? AppsFolderPath(appId) : program;
    }

    /// <summary>The shell path Start opens and draws the app by, and the shell names it by.</summary>
    public static string AppsFolderPath(string appId)
    {
        return @"shell:AppsFolder\" + appId;
    }

    /// <summary>
    /// Opens the app through its <see cref="AppsFolderPath"/>, as Start does. Null when Windows took it, otherwise
    /// why not.
    /// </summary>
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

    /// <summary>The program's name without <c>.exe</c> when the id is a program's file name, otherwise null.</summary>
    public static string? ProgramName(string appId)
    {
        return !IsPackaged(appId) && appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? appId[..^4]
            : null;
    }

    /// <summary>The name to show until a better one is found: the program's name, or else the id.</summary>
    public static string FallbackName(string appId)
    {
        return ProgramName(appId) ?? appId;
    }

    /// <summary>UserAssist stores some paths as <c>{known folder guid}\rest</c>. Other strings are returned as given.</summary>
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

    /// <summary>
    /// The description of the program at <paramref name="path"/>, as Task Manager shows it. Null when it has none or
    /// the file is gone.
    /// </summary>
    public static string? ProgramDescription(string path)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim() is { Length: > 0 } description
                ? description
                : null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
