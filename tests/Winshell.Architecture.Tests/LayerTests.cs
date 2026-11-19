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
            Rejected(layer, XDocument.Load(Path.Combine(Repo.Root, "src", layer, layer + ".csproj")), Layers.Skip(i + 1)));

        Assert.Empty(upward);
    }

    [Theory]
    [InlineData("Winshell.Generators", "OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\"", "")]
    [InlineData("Winshell.Generators", "OutputItemType=\"Analyzer\"",
        "Winshell.Core -> Winshell.Generators: an analyzer reference adds no assembly only with OutputItemType=\"Analyzer\" and ReferenceOutputAssembly=\"false\"")]
    [InlineData("Winshell.Generators", "ReferenceOutputAssembly=\"false\"", "Winshell.Core -> Winshell.Generators")]
    [InlineData("Winshell.Native", "", "")]
    [InlineData("Winshell.Native", "OutputItemType=\"Analyzer\"",
        "Winshell.Core -> Winshell.Native: an analyzer reference adds no assembly only with OutputItemType=\"Analyzer\" and ReferenceOutputAssembly=\"false\"")]
    public void A_project_reference_passes_below_its_layer_or_as_an_analyzer_that_adds_no_assembly(
        string referenced, string attributes, string expected)
    {
        var project = XDocument.Parse(
            $"<Project><ItemGroup><ProjectReference Include=\"../{referenced}/{referenced}.csproj\" {attributes} /></ItemGroup></Project>");

        Assert.Equal(expected, string.Concat(Rejected("Winshell.Core", project, ["Winshell.Native"])));
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

    private static IEnumerable<string> Rejected(string layer, XDocument project, IEnumerable<string> below)
    {
        return project.Descendants("ProjectReference")
            .Select(reference => (
                Name: Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value),
                Analyzer: reference.Attribute("OutputItemType")?.Value == "Analyzer",
                AddsAssembly: reference.Attribute("ReferenceOutputAssembly")?.Value != "false"))
            .Where(reference => reference.Analyzer ? reference.AddsAssembly : !below.Contains(reference.Name))
            .Select(reference => reference.Analyzer
                ? $"{layer} -> {reference.Name}: an analyzer reference adds no assembly only with OutputItemType=\"Analyzer\" and ReferenceOutputAssembly=\"false\""
                : $"{layer} -> {reference.Name}");
    }
}
