using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using ReactorComponent = Microsoft.UI.Reactor.Core.Component;

namespace Winshell.Tests;

public sealed partial class FeatureMapTests
{
    private static readonly string[] Kinds = ["command", "service", "widget", "block"];

    private static readonly string[] Shells = ["none", "features", "starter", "omarchy-style"];

    private static readonly string[] WinshellKeys = ["winshell", "exit", "output", "file", "within", "background", "for"];

    private static readonly string[] ElementKeys = ["element", "window", "has", "within"];

    private static readonly string MapPath = Path.Combine(Repo.Root, "features", "map.json");

    [Fact]
    public void Every_command_has_one_entry()
    {
        AssertMatches("command", Commands());
    }

    [Fact]
    public void Every_service_has_one_entry()
    {
        AssertMatches("service", Services());
    }

    [Fact]
    public void Every_widget_has_one_entry()
    {
        AssertMatches("widget", WidgetNames());
    }

    [Fact]
    public void Every_block_has_one_entry()
    {
        AssertMatches("block", Blocks());
    }

    [Fact]
    public void Every_entry_is_well_formed()
    {
        var problems = new List<string>();
        var entries = Entries();
        var ids = entries.Select(entry => Text(entry, "id")).ToList();
        foreach (var duplicate in ids.GroupBy(id => id).Where(group => group.Count() > 1))
            problems.Add($"{duplicate.Key} is in the map {duplicate.Count()} times. Keep one entry.");
        if (!ids.SequenceEqual(ids.Order(StringComparer.Ordinal)))
            problems.Add("The entries are not sorted by id. Sort them by id, ordinally.");
        foreach (var entry in entries)
            problems.AddRange(Problems(entry));
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void A_block_is_a_public_window_control_component_or_composition_of_the_shell_library()
    {
        Assert.Equal(
            ["Bar", "CaptureView", "FloatingWindow", "PanelWindow", "PopupWindow", "Surface", "WidgetArea", "WindowThumbnail"],
            Blocks());
    }

    private static List<string> Problems(JsonElement entry)
    {
        var id = Text(entry, "id");
        var kind = Text(entry, "kind");
        var shell = Text(entry, "shell");
        var problems = new List<string>();
        if (!Kinds.Contains(kind))
            problems.Add($"{id}: kind \"{kind}\" is not one of {string.Join(", ", Kinds)}.");
        if (!IdPattern().IsMatch(id) || !id.StartsWith(kind + ".", StringComparison.Ordinal))
            problems.Add($"{id}: an id is <kind>.<slug>, such as {kind}.my-feature, in lowercase with hyphens.");
        if (!Shells.Contains(shell))
            problems.Add($"{id}: shell \"{shell}\" is not one of {string.Join(", ", Shells)}.");
        if (Text(entry, "name").Length == 0)
            problems.Add($"{id}: name is empty. It is what the inventory of its kind calls it.");
        if (Text(entry, "summary").Length == 0)
            problems.Add($"{id}: summary is empty. Say what the driver proves.");
        var code = Array(entry, "code");
        if (code.Count == 0)
            problems.Add($"{id}: code is empty. List the files that implement it.");
        foreach (var path in code.Select(item => item.GetString() ?? ""))
            if (!File.Exists(Path.Combine(Repo.Root, path)))
                problems.Add($"{id}: code path {path} does not exist. Fix the path or remove it.");
        var steps = Array(entry, "steps");
        if (steps.Count == 0)
            problems.Add($"{id}: steps is empty. Add a step that drives the feature.");
        for (var i = 0; i < steps.Count; i++)
            problems.AddRange(StepProblems(id, i + 1, steps[i]));
        if (shell == "features" && kind is "service" or "block" && !steps.Any(step =>
                step.ValueKind == JsonValueKind.Object && step.TryGetProperty("element", out var element) &&
                element.GetString() == id))
            problems.Add(
                $"{id}: on the features shell, a step must find the element {id}. Give the feature shell's row that automation id.");
        return problems;
    }

    private static IEnumerable<string> StepProblems(string id, int number, JsonElement step)
    {
        if (step.ValueKind != JsonValueKind.Object)
        {
            yield return $"{id}: step {number} is not an object.";
            yield break;
        }

        var keys = step.EnumerateObject().Select(property => property.Name).ToList();
        var isWinshell = keys.Contains("winshell");
        var isElement = keys.Contains("element");
        if (isWinshell == isElement)
        {
            yield return $"{id}: step {number} needs exactly one of \"winshell\" and \"element\".";
            yield break;
        }

        var allowed = isWinshell ? WinshellKeys : ElementKeys;
        foreach (var key in keys.Where(key => !allowed.Contains(key)))
            yield return
                $"{id}: step {number} has \"{key}\", which a {(isWinshell ? "winshell" : "element")} step does not take. It takes {string.Join(", ", allowed)}.";
    }

    private static void AssertMatches(string kind, IReadOnlyCollection<string> inventory)
    {
        var mapped = Entries().Where(entry => Text(entry, "kind") == kind).Select(entry => Text(entry, "name"))
            .ToList();
        var problems = new List<string>();
        foreach (var name in inventory.Except(mapped, StringComparer.Ordinal))
            problems.Add(
                $"features/map.json has no {kind} entry named \"{name}\". Add one with id {kind}.{Slug(name)}, its code, a driver, and for a service or block a row in features/shell.");
        foreach (var name in mapped.Except(inventory, StringComparer.Ordinal))
            problems.Add(
                $"features/map.json has the {kind} entry \"{name}\", which winshell does not have. Remove it, or fix its name.");
        Assert.True(problems.Count == 0, string.Join('\n', problems));
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
            .Where(method => method.Name.StartsWith("New", StringComparison.Ordinal) && !method.IsSpecialName &&
                             InSubNamespace(method.ReturnType))
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
                .Select(type => type.Name)
                .Order(StringComparer.Ordinal)
        ];
    }

    private static string Slug(string name)
    {
        return SlugPattern().Replace(name.Replace('/', '-').Replace(' ', '-'), "$1-$2").ToLowerInvariant();
    }

    private static List<JsonElement> Entries()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(MapPath));
        return [.. document.RootElement.EnumerateArray().Select(entry => entry.Clone())];
    }

    private static string Text(JsonElement entry, string property)
    {
        return entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : "";
    }

    private static List<JsonElement> Array(JsonElement entry, string property)
    {
        return entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray()]
            : [];
    }

    [GeneratedRegex(@"^[a-z]+\.[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("""Widgets\.Register<\w+>\("([^"]+)"\)""")]
    private static partial Regex RegisterPattern();

    [GeneratedRegex(@"^public\s+(?:sealed\s+|static\s+|partial\s+)*(?:class|record)\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex PublicTypePattern();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex SlugPattern();
}
