using System.Text.RegularExpressions;
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
    private static readonly Regex ExistsCondition = new(
        @"^Exists\(\s*'(?<path>[^']*)'\s*\)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void Hand_written_code_in_a_WinUI_project_has_no_unsafe_code()
    {
        var projects = XDocument.Load(Path.Combine(Repo.Root, "winshell.slnx")).Descendants("Project")
            .Select(project => Path.Combine(Repo.Root, project.Attribute("Path")!.Value))
            .ToList();
        var unseen = new List<string>();
        var winui = projects.Where(path => UsesWinUI(path, unseen)).ToList();
        Assert.True(unseen.Count == 0, string.Join(Environment.NewLine, unseen));
        Assert.NotEmpty(winui);
        projects = winui;

        var found = projects.SelectMany(Sources).Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(UnsafeCode)
            .ToList();

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found));
    }

    // The example sets UseWinUI in the props file it imports, as a configuration from the package does.
    // The last assignment wins. A conditional true may hold, so it turns the project on; a conditional false
    // may not, so it cannot turn the project off. An import the guard cannot read fails the test, because a
    // UseWinUI inside it would never be scanned. Exists() of a file that is not there does not apply.
    private static bool UsesWinUI(string path, List<string> unseen)
    {
        return WinUISetting(path, Path.GetDirectoryName(Path.GetFullPath(path))!, unseen,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            ?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? WinUISetting(string path, string projectDirectory, List<string> unseen, HashSet<string> seen)
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
                if (imported is null)
                    continue;

                var condition = element.Attribute("Condition")?.Value;
                if (!ImportApplies(condition, folder, projectDirectory))
                    continue;

                var resolved = ResolveImport(imported, folder, projectDirectory);
                if (resolved is null)
                {
                    unseen.Add($"{Relative(full)} imports '{imported}', which the guard cannot read");
                    continue;
                }

                if (!File.Exists(resolved))
                {
                    unseen.Add($"{Relative(full)} imports '{imported}', which is not on disk");
                    continue;
                }

                var nested = WinUISetting(resolved, projectDirectory, unseen, seen);
                if (nested is null)
                    continue;

                // A condition other than Exists() might be false, so it may turn WinUI on but not off.
                if (condition is null || ExistsOnly(condition) ||
                    nested.Equals("true", StringComparison.OrdinalIgnoreCase))
                    setting = nested;
            }
            else if (element.Name == "UseWinUI")
            {
                var value = element.Value.Trim();
                if (element.Attribute("Condition") is null)
                    setting = value;
                else if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    setting = value;
            }

        return setting;
    }

    // $(WinshellWidgetsFile) is the widgets file beside the project when the property is empty, which is how
    // Winshell.props imports it. Anything else with a '$' is a path this guard cannot open.
    private static string? ResolveImport(string project, string fileDirectory, string projectDirectory)
    {
        var expanded = project
            .Replace("$(MSBuildThisFileDirectory)", WithSeparator(fileDirectory), StringComparison.OrdinalIgnoreCase)
            .Replace("$(MSBuildProjectDirectory)", projectDirectory, StringComparison.OrdinalIgnoreCase)
            .Replace("$(WinshellWidgetsFile)", Path.Combine(projectDirectory, "widgets.props"),
                StringComparison.OrdinalIgnoreCase);
        if (expanded.Contains('$'))
            return null;

        return Path.GetFullPath(Path.Combine(fileDirectory, expanded));
    }

    private static bool ImportApplies(string? condition, string fileDirectory, string projectDirectory)
    {
        if (condition is null || !ExistsOnly(condition))
            return true;

        var target = ResolveImport(ExistsPath(condition), fileDirectory, projectDirectory);
        return target is null || File.Exists(target);
    }

    private static bool ExistsOnly(string condition) => ExistsCondition.IsMatch(condition.Trim());

    private static string ExistsPath(string condition) => ExistsCondition.Match(condition.Trim()).Groups["path"].Value;

    private static string WithSeparator(string directory) =>
        directory.EndsWith(Path.DirectorySeparatorChar) || directory.EndsWith(Path.AltDirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;

    private static string Relative(string path) => Path.GetRelativePath(Repo.Root, path);

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
