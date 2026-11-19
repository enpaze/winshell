namespace Winshell.Package.Tests;

[TestClass]
public sealed class ImplicitUsingsTests
{
    private static readonly string[] PackageUsings =
    [
        "Winshell",
        "Microsoft.UI.Reactor",
        "Microsoft.UI.Reactor.Core",
        "static Microsoft.UI.Reactor.Factories",
        "static Winshell.Elements"
    ];

    [TestMethod]
    [DataRow("enable")]
    [DataRow("true")]
    public void A_project_with_implicit_usings_gets_the_package_usings_and_its_root_namespace(string implicitUsings)
    {
        var usings = Usings($"<ImplicitUsings>{implicitUsings}</ImplicitUsings><RootNamespace>MyShell</RootNamespace>");

        foreach (var expected in PackageUsings.Append("MyShell"))
            Assert.Contains(expected, usings);
    }

    [TestMethod]
    public void A_project_with_no_root_namespace_gets_only_the_package_usings()
    {
        var usings = Usings("<ImplicitUsings>enable</ImplicitUsings><RootNamespace></RootNamespace>");

        foreach (var expected in PackageUsings)
            Assert.Contains(expected, usings);
        Assert.AreEqual(PackageUsings.Length, usings.Count(u => !u.StartsWith("System", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void WinshellImplicitUsings_false_leaves_out_the_package_usings_and_keeps_the_SDKs()
    {
        var usings = Usings("""
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>MyShell</RootNamespace>
            <WinshellImplicitUsings>false</WinshellImplicitUsings>
            """);

        foreach (var unexpected in PackageUsings.Append("MyShell"))
            Assert.DoesNotContain(unexpected, usings);
        Assert.Contains("System", usings);
    }

    [TestMethod]
    public void A_project_without_implicit_usings_gets_none_of_the_package_usings()
    {
        var usings = Usings("<ImplicitUsings>disable</ImplicitUsings><RootNamespace>MyShell</RootNamespace>");

        foreach (var unexpected in PackageUsings.Append("MyShell"))
            Assert.DoesNotContain(unexpected, usings);
    }

    private static List<string> Usings(string properties)
    {
        return PackageProps.Items(properties, "Using")
            .Select(item => (item.TryGetProperty("Static", out var isStatic) && isStatic.GetString() == "true"
                ? "static "
                : "") + item.GetProperty("Identity").GetString())
            .ToList();
    }
}
