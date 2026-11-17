namespace Winshell.Apps;

public sealed record RecentDocument(string Name, string Path);

public sealed class RecentDocuments
{
    internal const int Limit = 20;

    private readonly string _folder;

    internal RecentDocuments(string folder)
    {
        _folder = folder;
    }

    public IReadOnlyList<RecentDocument> Search(string query)
    {
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || _folder.Length == 0 || !Directory.Exists(_folder))
            return [];
        var found = new List<RecentFile>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(_folder, "*.lnk"))
            {
                FileInfo info;
                try
                {
                    info = new FileInfo(path);
                    if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        continue;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(info.Name);
                if (name.Length == 0 || !words.All(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
                    continue;
                found.Add(new RecentFile(name, info.FullName, info.LastWriteTimeUtc));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        found.Sort(static (a, b) =>
        {
            var byTime = b.When.CompareTo(a.When);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.Name, b.Name);
        });
        return found.Take(Limit).Select(static file => new RecentDocument(file.Name, file.Path)).ToList();
    }

    private readonly record struct RecentFile(string Name, string Path, DateTime When);
}
