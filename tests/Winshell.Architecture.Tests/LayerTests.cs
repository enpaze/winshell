using System.Reflection;
using System.Xml.Linq;

namespace Winshell.Architecture.Tests;

[TestClass]
public class LayerTests
{
    private static readonly string[] Layers =
        ["Winshell.Shell", "Winshell.Services", "Winshell.Core", "Winshell.Native"];

    [TestMethod]
    public void A_project_references_only_layers_below_it()
    {
        var upward = Layers.SelectMany((layer, i) => XDocument
            .Load(Path.Combine(Repo.Root, "src", layer, layer + ".csproj"))
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
            .Where(name => !Layers.Skip(i + 1).Contains(name))
            .Select(name => $"{layer} -> {name}"));

        Assert.IsEmpty(upward);
    }

    [TestMethod]
    public void An_assembly_references_only_layers_below_it()
    {
        var upward = Layers.SelectMany((layer, i) => Assembly.Load(layer).GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("Winshell", StringComparison.OrdinalIgnoreCase) &&
                           !Layers.Skip(i + 1).Contains(name))
            .Select(name => $"{layer} -> {name}"));

        Assert.IsEmpty(upward);
    }
}
