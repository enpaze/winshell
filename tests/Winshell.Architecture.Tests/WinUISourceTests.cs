using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Winshell.Architecture.Tests;

/// <summary>
/// A WinUI project compiles with unsafe code allowed, because CsWinRT generates its marshalling only then, so the
/// compiler doesn't stop hand-written unsafe code there. Its assembly can't tell CsWinRT's unsafe code from ours, so
/// this reads the hand-written sources.
/// </summary>
public class WinUISourceTests
{
    [Fact]
    public void Hand_written_code_in_a_WinUI_project_has_no_unsafe_code()
    {
        var projects = XDocument.Load(Path.Combine(Repo.Root, "winshell.slnx")).Descendants("Project")
            .Select(project => Path.Combine(Repo.Root, project.Attribute("Path")!.Value))
            .Where(UsesWinUI)
            .ToList();
        Assert.NotEmpty(projects);

        var found = projects.SelectMany(Sources).Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(UnsafeCode)
            .ToList();

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found));
    }

    // The example sets UseWinUI in the props file it imports, as a configuration from the package does.
    // The last assignment wins.
    private static bool UsesWinUI(string path)
    {
        return WinUISetting(path, new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            ?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? WinUISetting(string path, HashSet<string> seen)
    {
        var full = Path.GetFullPath(path);
        if (!seen.Add(full))
            return null;

        string? setting = null;
        var folder = Path.GetDirectoryName(full)!;
        foreach (var element in XDocument.Load(full).Descendants())
            if (element.Name == "Import")
            {
                var imported = element.Attribute("Project")?.Value;
                if (imported is null || imported.Contains('$') || element.Attribute("Condition") is not null)
                    continue;

                var nested = WinUISetting(Path.Combine(folder, imported), seen);
                if (nested is not null)
                    setting = nested;
            }
            else if (element.Name == "UseWinUI" && element.Attribute("Condition") is null)
            {
                setting = element.Value.Trim();
            }

        return setting;
    }

    // The project's folder without the build output and generated code in bin and obj, and the files it links.
    private static IEnumerable<string> Sources(string project)
    {
        var folder = Path.GetDirectoryName(project)!;
        var own = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
                Path.GetRelativePath(folder, file).Split(Path.DirectorySeparatorChar)[0] is not ("bin" or "obj"));
        var linked = XDocument.Load(project).Descendants("Compile")
            .Select(compile => compile.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(include => Path.GetFullPath(Path.Combine(folder, include)));
        return own.Concat(linked);
    }

    // Read as a Release and as a Debug build compile it, so code under #if DEBUG counts too.
    private static IEnumerable<string> UnsafeCode(string file)
    {
        return new[] { CSharpParseOptions.Default, CSharpParseOptions.Default.WithPreprocessorSymbols("DEBUG") }
            .SelectMany(options => CSharpSyntaxTree.ParseText(File.ReadAllText(file), options, file).GetRoot()
                .DescendantNodesAndTokens())
            .Where(part => part.Kind() is SyntaxKind.UnsafeKeyword or SyntaxKind.FixedKeyword or SyntaxKind.PointerType
                or SyntaxKind.FunctionPointerType)
            .Select(part =>
                $"{Path.GetRelativePath(Repo.Root, file)}({part.GetLocation()!.GetLineSpan().StartLinePosition.Line + 1}): {part}")
            .Distinct();
    }
}
