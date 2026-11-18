using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using ReactorComponent = Microsoft.UI.Reactor.Core.Component;

namespace Winshell.Tests;

public sealed partial class FeatureMapTests
{
    private const string RelativeMap = ".agents/skills/verify-winshell/features";

    private static readonly string MapFolder = Path.Combine(Repo.Root, RelativeMap);

    private static readonly Dictionary<Type, Type> ComponentWindows = new()
    {
        [typeof(PanelWindow<>)] = typeof(PanelWindow),
        [typeof(PanelWindow<,>)] = typeof(PanelWindow),
        [typeof(PopupWindow<>)] = typeof(PopupWindow),
        [typeof(PopupWindow<,>)] = typeof(PopupWindow),
        [typeof(FloatingWindow<>)] = typeof(FloatingWindow),
        [typeof(FloatingWindow<,>)] = typeof(FloatingWindow)
    };

    [Fact]
    public void Every_command_has_a_feature_file()
    {
        AssertMatches("command", Commands());
    }

    [Fact]
    public void Every_service_has_a_feature_file()
    {
        AssertMatches("service", Services());
    }

    [Fact]
    public void Every_widget_has_a_feature_file()
    {
        AssertMatches("widget", WidgetNames());
    }

    [Fact]
    public void Every_block_has_a_feature_file()
    {
        AssertMatches("block", Blocks());
    }

    [Fact]
    public void Every_feature_file_is_of_a_kind_the_inventory_has()
    {
        var kinds = new[] { "command", "service", "widget", "block" };
        var strays = FeatureIds().Where(id => !kinds.Contains(id.Split('.')[0])).ToList();
        Assert.True(strays.Count == 0,
            string.Join('\n', strays.Select(id =>
                $"{id}.md in {RelativeMap} is not a command, service, widget or block. Rename it <kind>.<slug>.md, or remove it.")));
    }

    [Fact]
    public void A_block_is_a_public_window_control_component_or_composition_of_the_shell_library()
    {
        Assert.Equal(
            ["Bar", "CaptureView", "FloatingWindow", "Launcher", "PanelWindow", "PopupWindow", "Surface", "WidgetArea", "WindowThumbnail"],
            Blocks());
    }

    [Fact]
    public void Every_component_window_is_a_component_sub_feature_of_its_windows_feature_file()
    {
        var problems = new List<string>();
        foreach (var (window, block) in ComponentWindows)
        {
            var file = $"{RelativeMap}/block.{Slug(block.Name)}.md";
            var shown = $"`{block.Name}<{string.Join(", ", window.GetGenericArguments().Select(type => type.Name))}>`";
            var line = File.ReadLines(Path.Combine(Repo.Root, file))
                .FirstOrDefault(line => line.StartsWith("- `component` ", StringComparison.Ordinal));
            if (line is null || !line.Contains(shown, StringComparison.Ordinal))
                problems.Add(
                    $"{file} does not name {shown} in its `component` sub-feature. Add it to the line that starts with - `component`.");
        }

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    private static void AssertMatches(string kind, IReadOnlyCollection<string> inventory)
    {
        var expected = inventory.ToDictionary(name => $"{kind}.{Slug(name)}", StringComparer.Ordinal);
        var mapped = FeatureIds().Where(id => id.StartsWith(kind + ".", StringComparison.Ordinal)).ToList();
        var problems = new List<string>();
        foreach (var (id, name) in expected.Where(pair => !mapped.Contains(pair.Key)))
            problems.Add(
                $"{RelativeMap} has no file for the {kind} \"{name}\". Add {id}.md in the shape {RelativeMap}/README.md describes, with its line in the index, and for a service or block a row in .agents/skills/verify-winshell/shell. Then run drive.ps1 -Id {id}.");
        foreach (var id in mapped.Where(id => !expected.ContainsKey(id)))
            problems.Add(
                $"{RelativeMap}/{id}.md maps a {kind} that winshell does not have. Remove the file and its index line, or rename it to the {kind}'s <kind>.<slug>.md.");
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    private static List<string> FeatureIds()
    {
        return
        [
            .. Directory.EnumerateFiles(MapFolder, "*.md")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Where(name => name != "README")
                .Order(StringComparer.Ordinal)
        ];
    }

    private static List<string> Commands()
    {
        return
        [
            .. CliParser.Entries.SelectMany(entry => entry.Subcommands.Length == 0
                ? [entry.Name]
                : entry.Subcommands.Select(sub => $"{entry.Name} {sub.Name}"))
        ];
    }

    private static List<string> Services()
    {
        const BindingFlags members = BindingFlags.Public | BindingFlags.Instance;
        var properties = typeof(Shell).GetProperties(members).Where(property => InSubNamespace(property.PropertyType))
            .Select(property => property.Name);
        var factories = typeof(Shell).GetMethods(members)
            .Where(method => !method.IsSpecialName && InSubNamespace(method.ReturnType))
            .Select(method => method.Name);
        return [.. properties.Concat(factories).Order(StringComparer.Ordinal)];
    }

    private static bool InSubNamespace(Type type)
    {
        return type.Namespace?.StartsWith("Winshell.", StringComparison.Ordinal) == true;
    }

    private static List<string> WidgetNames()
    {
        var names = Registered(Path.Combine(Repo.Root, "templates", "winshell")).Select(name => "starter/" + name);
        foreach (var example in Directory.EnumerateDirectories(Path.Combine(Repo.Root, "examples")))
            names = names.Concat(Registered(example).Select(name => $"{Path.GetFileName(example)}/{name}"));
        return [.. names.Order(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> Registered(string folder)
    {
        return Sources(folder).SelectMany(file => RegisterPattern().Matches(File.ReadAllText(file)))
            .Select(match => match.Groups[1].Value);
    }

    private static IEnumerable<string> Sources(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs"))
            yield return file;
        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            if (Path.GetFileName(child) is "bin" or "obj")
                continue;
            foreach (var file in Sources(child))
                yield return file;
        }
    }

    private static List<string> Blocks()
    {
        var compositions = Directory.EnumerateFiles(Path.Combine(Repo.Root, "src", "Winshell.Shell", "Compositions"), "*.cs")
            .SelectMany(file => PublicTypePattern().Matches(File.ReadAllText(file)))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        return
        [
            .. typeof(Shell).Assembly.GetExportedTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false } &&
                               (type.IsSubclassOf(typeof(ShellWindow)) || type.IsSubclassOf(typeof(UIElement)) ||
                                type.IsSubclassOf(typeof(ReactorComponent)) || compositions.Contains(type.Name)))
                .Select(type => ComponentWindows.GetValueOrDefault(type, type).Name)
                .Distinct()
                .Order(StringComparer.Ordinal)
        ];
    }

    private static string Slug(string name)
    {
        return SlugPattern().Replace(name.Replace('/', '-').Replace(' ', '-'), "$1-$2").ToLowerInvariant();
    }

    [GeneratedRegex("""Widgets\.Register<\w+>\("([^"]+)"\)""")]
    private static partial Regex RegisterPattern();

    [GeneratedRegex(@"^public\s+(?:sealed\s+|static\s+|partial\s+)*(?:class|record)\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex PublicTypePattern();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex SlugPattern();
}
