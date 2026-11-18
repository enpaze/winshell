using Winshell.Docs;

namespace Winshell.ShellTests;

public sealed class DocsTests
{
    private static readonly string Repo = TestSupport.Repo.Root;

    private static readonly string WidgetLibrary = Path.Combine(Repo, "tests", "Winshell.Package.Tests", "MessageWidget");

    [Fact]
    public void Every_csharp_example_is_compiled()
    {
        var files = Directory.GetFiles(Path.Combine(Repo, "templates", "winshell"), "*.cs")
            .Append(Path.Combine(Repo, "tests", "Winshell.Shell.Tests", "GuideSamples.cs"))
            .Append(Path.Combine(WidgetLibrary, "Message.cs"));
        var source = string.Join('\n', files.Select(File.ReadAllText)).Replace("\r\n", "\n", StringComparison.Ordinal);
        var examples = Examples().ToList();
        Assert.NotEmpty(examples);
        var problems = new List<string>();
        foreach (var (file, line, code) in examples)
            if (code.Length == 0)
                problems.Add($"{Path.GetRelativePath(Repo, file)}:{line}: empty csharp fence");
            else if (!source.Contains(code, StringComparison.Ordinal))
                problems.Add($"{Path.GetRelativePath(Repo, file)}:{line}: csharp fence is not in a file the solution builds");
        Assert.True(problems.Count == 0, "C# examples that are not compiled:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void The_guide_shows_the_widget_library_project_the_package_tests_build()
    {
        var guide = File.ReadAllText(Path.Combine(Repo, "docs", "configuration.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        var project = File.ReadAllText(Path.Combine(WidgetLibrary, "MessageWidget.csproj"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("```xml\n" + project + "```\n", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void The_starter_and_the_guide_call_no_Watch()
    {
        var files = SourceFiles(Path.Combine(Repo, "templates", "winshell"))
            .Append(Path.Combine(Repo, "docs", "configuration.md"))
            .Append(Path.Combine(Repo, "tests", "Winshell.Shell.Tests", "GuideSamples.cs"));
        var watched = files
            .Where(file => File.ReadAllText(file).Contains(".Watch(", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(Repo, file))
            .ToList();
        Assert.True(watched.Count == 0, "Watch calls:\n" + string.Join("\n", watched));
    }

    private static IEnumerable<string> SourceFiles(string root)
    {
        var bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        var obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !file.Contains(bin, StringComparison.OrdinalIgnoreCase)
                && !file.Contains(obj, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_reference_is_what_the_generator_writes()
    {
        var folder = Path.Combine(Repo, "docs", "reference");
        var stale = ReferencePages.Stale(folder, ReferencePages.Generate(folder));
        Assert.True(stale.Count == 0,
            $"Out of date in docs/reference: {string.Join(", ", stale)}. Run: dotnet run scripts/generate-reference.cs");
    }

    [Fact]
    public void The_reference_keeps_a_ref_parameter_s_modifier()
    {
        var folder = Path.Combine(Repo, "docs", "reference");
        var page = ReferencePages.Generate(folder)["winshell.md"];

        Assert.Contains("| `Set<T>(ref T? field, T? value, string property = \"\")` | bool, protected |", page,
            StringComparison.Ordinal);
    }

    private static IEnumerable<(string File, int Line, string Code)> Examples()
    {
        foreach (var file in Pages())
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() != "```csharp")
                    continue;
                var start = i + 1;
                var end = start;
                while (end < lines.Length && lines[end].Trim() != "```")
                    end++;
                yield return (file, start + 1, string.Join('\n', lines[start..end]));
                i = end;
            }
        }
    }

    private static IEnumerable<string> Pages()
    {
        var agents = Path.Combine(Repo, "docs", "agents") + Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(Repo, "docs"), "*.md", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(agents, StringComparison.OrdinalIgnoreCase))
            .Append(Path.Combine(Repo, "README.md"))
            .Order(StringComparer.Ordinal);
    }
}
