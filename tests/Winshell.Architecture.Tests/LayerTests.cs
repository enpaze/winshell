using System.Reflection;
using System.Xml.Linq;

namespace Winshell.Architecture.Tests;

public class LayerTests
{
    private static readonly string[] Layers =
        ["Winshell.Shell", "Winshell.Services", "Winshell.Core", "Winshell.Native"];

    [Fact]
    public void A_project_references_only_layers_below_it()
    {
        var upward = Layers.SelectMany((layer, i) =>
            Upward(layer, XDocument.Load(Path.Combine(Repo.Root, "src", layer, layer + ".csproj")), Layers.Skip(i + 1)));

        Assert.Empty(upward);
    }

    [Theory]
    [InlineData("OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\"", "")]
    [InlineData("OutputItemType=\"Analyzer\"",
        "Winshell.Core -> Winshell.Generators: an analyzer reference adds no assembly only with OutputItemType=\"Analyzer\" and ReferenceOutputAssembly=\"false\"")]
    [InlineData("ReferenceOutputAssembly=\"false\"", "Winshell.Core -> Winshell.Generators")]
    public void A_project_reference_above_its_layer_passes_only_as_an_analyzer_that_adds_no_assembly(
        string attributes, string expected)
    {
        var project = XDocument.Parse(
            $"<Project><ItemGroup><ProjectReference Include=\"../Winshell.Generators/Winshell.Generators.csproj\" {attributes} /></ItemGroup></Project>");

        Assert.Equal(expected, string.Concat(Upward("Winshell.Core", project, ["Winshell.Native"])));
    }

    [Fact]
    public void An_assembly_references_only_layers_below_it()
    {
        var upward = Layers.SelectMany((layer, i) => Assembly.Load(layer).GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("Winshell", StringComparison.OrdinalIgnoreCase) &&
                           !Layers.Skip(i + 1).Contains(name))
            .Select(name => $"{layer} -> {name}"));

        Assert.Empty(upward);
    }

    private static IEnumerable<string> Upward(string layer, XDocument project, IEnumerable<string> below)
    {
        return project.Descendants("ProjectReference")
            .Select(reference => (
                Name: Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value),
                Analyzer: reference.Attribute("OutputItemType")?.Value == "Analyzer",
                AddsAssembly: reference.Attribute("ReferenceOutputAssembly")?.Value != "false"))
            .Where(reference => !below.Contains(reference.Name) && (!reference.Analyzer || reference.AddsAssembly))
            .Select(reference => reference.Analyzer
                ? $"{layer} -> {reference.Name}: an analyzer reference adds no assembly only with OutputItemType=\"Analyzer\" and ReferenceOutputAssembly=\"false\""
                : $"{layer} -> {reference.Name}");
    }
}
