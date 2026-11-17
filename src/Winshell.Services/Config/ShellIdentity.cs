using System.Security.Cryptography;
using System.Text;

namespace Winshell;

internal sealed record ShellIdentity(string ShellId, string DataDir, string StateDir, string CacheDir)
{
    private IReadOnlyList<(string From, string To)> Earlier { get; init; } = [];

    public static ShellIdentity Resolve(string program, ShellLocations locations, string? shellId, string? dataDir,
        string? stateDir, string? cacheDir)
    {
        var id = shellId ?? DefaultId(program, locations.ConfigRoot);
        var earlier = shellId is null ? Hashed(program) : id;
        var relativeTo = ConfigurationProject.FolderOf(program) ?? Path.GetDirectoryName(program)!;
        var moves = new List<(string, string)>();

        string Folder(string? given, string root)
        {
            if (Expand(given, root, relativeTo) is { } folder)
                return folder;
            if (earlier != id)
                moves.Add((Path.Combine(root, "by-shell", earlier), Path.Combine(root, "by-shell", id)));
            return Path.Combine(root, "by-shell", id);
        }

        return new ShellIdentity(id, Folder(dataDir, locations.DataRoot), Folder(stateDir, locations.StateRoot),
            Folder(cacheDir, locations.CacheRoot))
        { Earlier = moves };
    }

    private static string DefaultId(string program, string configRoot)
    {
        if (configRoot.Length > 0 && ConfigurationProject.FolderOf(program) is { } folder)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configRoot));
            if (string.Equals(folder, root, StringComparison.OrdinalIgnoreCase))
                return "root";
            if (string.Equals(Path.GetDirectoryName(folder), root, StringComparison.OrdinalIgnoreCase))
                return Path.GetFileName(folder).ToLowerInvariant();
        }

        return Hashed(program);
    }

    private static string Hashed(string program)
    {
        var path = ConfigurationProject.InOutputFolder(program) ?? program;
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant())))[..32];
    }

    public void CarryOver(Log log)
    {
        foreach (var (from, to) in Earlier)
        {
            if (!Directory.Exists(from) || Path.Exists(to))
                continue;
            try
            {
                Directory.Move(from, to);
                log.Info("shell", $"Moved \"{from}\" to \"{to}\"");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log.Warn("shell", $"Could not move \"{from}\" to \"{to}\", so it stays where it is: {e.Message}");
            }
        }
    }

    private static string? Expand(string? folder, string baseDir, string relativeTo)
    {
        if (folder is null)
            return null;
        var text = Environment.ExpandEnvironmentVariables(folder.Replace("$BASE", baseDir, StringComparison.Ordinal));
        if (text.Length == 0)
            return Path.GetFullPath(text);
        if (!Path.IsPathRooted(text))
            text = Path.Combine(relativeTo, text);
        return Path.GetFullPath(text);
    }
}
