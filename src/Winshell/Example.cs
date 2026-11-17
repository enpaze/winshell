using System.Collections.Immutable;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Winshell;

internal sealed class Example
{
    internal const string RepoOnly = "<!-- winshell new leaves this section out -->";

    private const string ResourcePrefix = "new/";

    private readonly ImmutableArray<(string Path, string Resource)> _files;

    private Example(string name, string description, ImmutableArray<(string Path, string Resource)> files)
    {
        Name = name;
        Description = description;
        _files = files;
    }

    public static ImmutableArray<Example> All { get; } = Load();

    public string Name { get; }

    public string Description { get; }

    public bool CopyTo(string folder, string version, string packages)
    {
        Directory.CreateDirectory(folder);
        foreach (var (path, resource) in _files)
        {
            var target = Path.Combine(folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var stream = Open(resource);
            if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(target, Project(Read(stream), version));
            }
            else if (path.Equals("README.md", StringComparison.OrdinalIgnoreCase))
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
            .. byName.Select(pair => new Example(pair.Key, Describe(pair.Key, pair.Value), [.. pair.Value]))
                .OrderBy(example => example.Name != "starter")
        ];
    }

    private static string Describe(string name, List<(string Path, string Resource)> files)
    {
        if (name == "starter")
            return "A bar with a clock and a battery";
        var readme = files.FirstOrDefault(file => file.Path.Equals("README.md", StringComparison.OrdinalIgnoreCase));
        if (readme.Resource is null)
            return "";
        using var stream = Open(readme.Resource);
        var title = Read(stream).Split('\n').FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
        return title is not { } heading ? "" : heading[2..].Trim();
    }

    private static string Project(string text, string version)
    {
        var root = XDocument.Parse(text).Root!;
        foreach (var import in root.Elements("Import")
                     .Where(e => ((string?)e.Attribute("Project"))?.EndsWith("Winshell.props",
                         StringComparison.OrdinalIgnoreCase) == true)
                     .ToList())
        {
            if (import.PreviousNode is XComment comment)
                comment.Remove();
            import.Remove();
        }

        foreach (var reference in root.Descendants("ProjectReference")
                     .Where(e => ((string?)e.Attribute("Include"))?.EndsWith("Winshell.Shell.csproj",
                         StringComparison.OrdinalIgnoreCase) == true)
                     .ToList())
            reference.ReplaceWith(new XElement("PackageReference", new XAttribute("Include", "Winshell")));

        foreach (var package in root.Descendants("PackageReference")
                     .Where(e => (string?)e.Attribute("Include") == "Winshell"))
            package.SetAttributeValue("Version", version);

        var output = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n"
        };
        using (var writer = XmlWriter.Create(output, settings))
        {
            root.Save(writer);
        }

        return output.Append('\n').ToString();
    }

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
