using Winshell.Docs;

namespace Winshell.ShellTests;

/// <summary>
/// Each reference page is generated from the public API of <c>Winshell.Shell</c>, <c>Winshell.Services</c> and
/// <c>Winshell.Core</c>. Each <c>csharp</c> sample in the docs and the README is text from <see cref="GuideSamples"/>,
/// which this project builds, from the message widget, which the solution builds, or from the starter in
/// <c>templates/winshell</c>, which <c>scripts/build-template.ps1</c> builds.
/// </summary>
public sealed class DocsTests
{
    private static readonly string Repo = TestSupport.Repo.Root;

    [Fact]
    public void Every_csharp_example_is_compiled()
    {
        var files = Directory.GetFiles(Path.Combine(Repo, "templates", "winshell"), "*.cs")
            .Append(Path.Combine(Repo, "tests", "Winshell.Shell.Tests", "GuideSamples.cs"))
            .Append(Path.Combine(Repo, "docs", "samples", "message-widget", "Message.cs"));
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

    /// <summary>
    /// The starter, the configuration guide and the samples teach components. A <c>Watch</c> call or a XAML file in
    /// the message sample would put the old widget shape back in front of an author.
    /// </summary>
    [Fact]
    public void The_starter_the_guide_and_the_samples_call_no_Watch_and_the_message_sample_has_no_xaml()
    {
        var files = SourceFiles(Path.Combine(Repo, "templates", "winshell"))
            .Append(Path.Combine(Repo, "docs", "configuration.md"))
            .Append(Path.Combine(Repo, "tests", "Winshell.Shell.Tests", "GuideSamples.cs"))
            .Concat(SourceFiles(Path.Combine(Repo, "docs", "samples")));
        var watched = files
            .Where(file => File.ReadAllText(file).Contains(".Watch(", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(Repo, file))
            .ToList();
        var xaml = SourceFiles(Path.Combine(Repo, "docs", "samples", "message-widget"))
            .Where(file => file.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(Repo, file))
            .ToList();
        Assert.True(watched.Count == 0 && xaml.Count == 0,
            "Watch calls:\n" + string.Join("\n", watched) + "\nXAML:\n" + string.Join("\n", xaml));
    }

    /// <summary>Source under <paramref name="root"/>, leaving out build output a compiled sample leaves behind.</summary>
    private static IEnumerable<string> SourceFiles(string root)
    {
        var bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        var obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !file.Contains(bin, StringComparison.OrdinalIgnoreCase)
                && !file.Contains(obj, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Each <c>docs/reference/winshell*.md</c> page is what <c>scripts/generate-reference.cs</c> writes from the
    /// public API and its /// comments. This project compiles the page writer, <c>scripts/reference-pages.cs</c>, so
    /// the pages can't drift from the code.
    /// </summary>
    [Fact]
    public void The_reference_is_what_the_generator_writes()
    {
        var folder = Path.Combine(Repo, "docs", "reference");
        var stale = ReferencePages.Stale(folder, ReferencePages.Generate(folder));
        Assert.True(stale.Count == 0,
            $"Out of date in docs/reference: {string.Join(", ", stale)}. Run: dotnet run scripts/generate-reference.cs");
    }

    /// <summary>The <c>```csharp</c> blocks of every page, with the line each one starts on.</summary>
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

    /// <summary>The pages in <c>docs/</c>, outside <c>docs/agents/</c>, and the README.</summary>
    private static IEnumerable<string> Pages()
    {
        var agents = Path.Combine(Repo, "docs", "agents") + Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(Repo, "docs"), "*.md", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(agents, StringComparison.OrdinalIgnoreCase))
            .Append(Path.Combine(Repo, "README.md"))
            .Order(StringComparer.Ordinal);
    }
}
