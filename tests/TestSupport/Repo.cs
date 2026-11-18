using System.Text.Json;

namespace Winshell.TestSupport;

internal static class Repo
{
    public static string Root { get; } = Find();

    public static string ReleaseVersion()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, ".release-please-manifest.json")));
        return json.RootElement.GetProperty(".").GetString() ??
               throw new InvalidOperationException("The release-please manifest has no version.");
    }

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "winshell.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("The tests run outside the repository.");
    }
}
