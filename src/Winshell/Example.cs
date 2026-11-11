using System.Collections.Immutable;
using System.Xml.Linq;

namespace Winshell;

/// <summary>
/// A configuration <c>winshell new</c> can copy: the <c>dotnet new</c> starter, or a folder under <c>examples</c>. Each
/// is built into winshell.exe, so it is always this release's.
/// </summary>
internal sealed class Example
{
    /// <summary>The marker, the first line under a README heading that isn't blank, that leaves that section out of a copy.</summary>
    internal const string RepoOnly = "<!-- winshell new leaves this section out -->";

    private const string ResourcePrefix = "new/";

    private readonly ImmutableArray<(string Path, string Resource)> _files;

    private Example(string name, string description, ImmutableArray<(string Path, string Resource)> files)
    {
        Name = name;
        Description = description;
        _files = files;
    }

    /// <summary>The starter, then every example by name.</summary>
    public static ImmutableArray<Example> All { get; } = Load();

    public string Name { get; }

    /// <summary>The title of its README.</summary>
    public string Description { get; }

    /// <summary>
    /// Writes this configuration into <paramref name="folder"/>. The project winshell.exe carries already references
    /// the Winshell package of this release. README sections marked <see cref="RepoOnly"/> are left out. When
    /// <paramref name="packages"/> holds that package at <paramref name="version"/>, a <c>nuget.config</c> adds it as
    /// a source.
    /// </summary>
    /// <returns>Whether that <c>nuget.config</c> was written.</returns>
    public bool CopyTo(string folder, string version, string packages)
    {
        Directory.CreateDirectory(folder);
        foreach (var (path, resource) in _files)
        {
            var target = Path.Combine(folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var stream = Open(resource);
            if (path.Equals("README.md", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(target, WithoutRepoOnly(Read(stream)));
            }
            else
            {
                using var file = File.Create(target);
                stream.CopyTo(file);
            }
        }

        if (!File.Exists(Path.Combine(packages, $"Winshell.{version}.nupkg")))
            return false;
        File.WriteAllText(Path.Combine(folder, "nuget.config"), NuGetConfig(packages));
        return true;
    }

    /// <summary>
    /// The resources are <c>new/&lt;name&gt;/&lt;path&gt;</c>. Winshell.csproj names them, from <c>templates/winshell</c>
    /// as <c>starter</c> and from each folder under <c>examples</c>.
    /// </summary>
    private static ImmutableArray<Example> Load()
    {
        var byName = new SortedDictionary<string, List<(string, string)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in typeof(Example).Assembly.GetManifestResourceNames())
        {
            var name = resource.Replace('\\', '/');
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                continue;
            var parts = name[ResourcePrefix.Length..].Split('/', 2);
            if (parts.Length < 2)
                continue;
            if (!byName.TryGetValue(parts[0], out var files))
                byName[parts[0]] = files = [];
            files.Add((parts[1].Replace('/', Path.DirectorySeparatorChar), resource));
        }

        return
        [
            .. byName.Select(pair => new Example(pair.Key, Describe(pair.Value), [.. pair.Value]))
                .OrderBy(example => example.Name != "starter")
        ];
    }

    private static string Describe(List<(string Path, string Resource)> files)
    {
        var readme = files.FirstOrDefault(file => file.Path.Equals("README.md", StringComparison.OrdinalIgnoreCase));
        if (readme.Resource is null)
            return "";
        using var stream = Open(readme.Resource);
        var title = Read(stream).Split('\n').FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
        return title is not { } heading ? "" : heading[2..].Trim();
    }

    /// <summary>
    /// Leaves out each section whose first line that isn't blank is <see cref="RepoOnly"/>, from its heading up to the
    /// next heading as high.
    /// </summary>
    private static string WithoutRepoOnly(string text)
    {
        var lines = text.Split('\n');
        var kept = new List<string>();
        var skipping = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var level = HeadingLevel(lines[i]);
            if (skipping > 0 && level > 0 && level <= skipping)
                skipping = 0;
            if (skipping == 0 && level > 0 &&
                lines.Skip(i + 1).FirstOrDefault(line => line.Trim().Length > 0)?.Trim() == RepoOnly)
                skipping = level;
            if (skipping == 0)
                kept.Add(lines[i]);
        }

        return string.Join('\n', kept);
    }

    private static int HeadingLevel(string line)
    {
        var level = line.TakeWhile(c => c == '#').Count();
        return level > 0 && line.Length > level && line[level] == ' ' ? level : 0;
    }

    private static string NuGetConfig(string packages)
    {
        var config = new XElement("configuration",
            new XElement("packageSources",
                new XElement("add", new XAttribute("key", "winshell"), new XAttribute("value", packages))));
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + config.ToString().ReplaceLineEndings("\n") + "\n";
    }

    private static Stream Open(string resource)
    {
        return typeof(Example).Assembly.GetManifestResourceStream(resource)
               ?? throw new InvalidOperationException($"winshell.exe has no resource {resource}.");
    }

    private static string Read(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
