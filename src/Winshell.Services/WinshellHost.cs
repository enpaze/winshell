using Winshell.Native;

namespace Winshell;

internal static class WinshellHost
{
    private const string ConsoleName = "winshell.exe";

    private const string WindowlessName = "winshellw.exe";

    public static bool Is(string path)
    {
        return Named(path, ConsoleName) || Named(path, WindowlessName);
    }

    public static bool IsWindowless(string? path)
    {
        return path is not null && Named(path, WindowlessName);
    }

    public static string ConsoleOf(string path)
    {
        if (!IsWindowless(path))
            return path;
        var folder = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(folder) ? ConsoleName : Path.Combine(folder, ConsoleName);
    }

    public static string? Supervising()
    {
        return ProcessImage.Ancestor(Is);
    }

    private static bool Named(string path, string name)
    {
        return Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase);
    }
}
