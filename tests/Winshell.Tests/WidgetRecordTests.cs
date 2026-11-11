using System.Text.RegularExpressions;
using Winshell.Commands;

namespace Winshell.Tests;

/// <summary>
/// <c>Winshell.props</c> keeps its own literals for the three record names it reads. MSBuild item and property names
/// are XML identifiers, so a C# constant cannot supply them. This ties those literals to the CLI type that owns them.
/// </summary>
public sealed partial class WidgetRecordTests
{
    [GeneratedRegex(@"<(?<name>\w+) Condition=""'\$\(\k<name>\)' == ''"">\$\(MSBuildProjectDirectory\)\\widgets\.props</\k<name>>")]
    private static partial Regex DefaultProperty();

    [GeneratedRegex(@"<Import Project=""\$\((?<name>\w+)\)"" Condition=""Exists\('\$\(\k<name>\)'\)""")]
    private static partial Regex ImportedProperty();

    [GeneratedRegex(@"Condition=""'@\((?<name>\w+)\)' (==|!=) ''""")]
    private static partial Regex ItemCondition();

    [GeneratedRegex(@"Widgets=""@\((?<name>\w+)\)""")]
    private static partial Regex RegisteredItem();

    [GeneratedRegex(@"GetMetadata\(""(?<name>[^""]+)""\)")]
    private static partial Regex ItemMetadata();

    [Fact]
    public void The_package_targets_use_the_widget_record_names()
    {
        var props = File.ReadAllText(Path.Combine(Repo.Root, "src", "Winshell.Package", "buildTransitive", "Winshell.props"));

        var property = DefaultProperty().Match(props);
        Assert.True(property.Success);
        Assert.Equal(WidgetRecord.Property, property.Groups["name"].Value);

        var imported = ImportedProperty().Match(props);
        Assert.True(imported.Success);
        Assert.Equal(WidgetRecord.Property, imported.Groups["name"].Value);

        var conditions = ItemCondition().Matches(props);
        Assert.NotEmpty(conditions);
        Assert.All(conditions, match => Assert.Equal(WidgetRecord.Item, match.Groups["name"].Value));

        var registered = RegisteredItem().Match(props);
        Assert.True(registered.Success);
        Assert.Equal(WidgetRecord.Item, registered.Groups["name"].Value);

        var metadata = ItemMetadata().Match(props);
        Assert.True(metadata.Success);
        Assert.Equal(WidgetRecord.Metadata, metadata.Groups["name"].Value);
    }
}
