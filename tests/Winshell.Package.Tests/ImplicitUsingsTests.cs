using System.Text.Json;

namespace Winshell.Package.Tests;

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

    [Theory]
    [InlineData("enable")]
    [InlineData("true")]
    public void A_project_with_implicit_usings_gets_the_package_usings_and_its_root_namespace(string implicitUsings)
    {
        var usings = Usings($"<ImplicitUsings>{implicitUsings}</ImplicitUsings><RootNamespace>MyShell</RootNamespace>");

        Assert.All(PackageUsings.Append("MyShell"), expected => Assert.Contains(expected, usings));
    }

    [Fact]
    public void A_project_with_no_root_namespace_gets_only_the_package_usings()
    {
        var usings = Usings("<ImplicitUsings>enable</ImplicitUsings><RootNamespace></RootNamespace>");

        Assert.All(PackageUsings, expected => Assert.Contains(expected, usings));
        Assert.Equal(PackageUsings.Length, usings.Count(u => !u.StartsWith("System", StringComparison.Ordinal)));
    }

    [Fact]
    public void WinshellImplicitUsings_false_leaves_out_the_package_usings_and_keeps_the_SDKs()
    {
        var usings = Usings("""
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>MyShell</RootNamespace>
            <WinshellImplicitUsings>false</WinshellImplicitUsings>
            """);

        Assert.All(PackageUsings.Append("MyShell"), unexpected => Assert.DoesNotContain(unexpected, usings));
        Assert.Contains("System", usings);
    }

    [Fact]
    public void A_project_without_implicit_usings_gets_none_of_the_package_usings()
    {
        var usings = Usings("<ImplicitUsings>disable</ImplicitUsings><RootNamespace>MyShell</RootNamespace>");

        Assert.All(PackageUsings.Append("MyShell"), unexpected => Assert.DoesNotContain(unexpected, usings));
    }

    private static List<string> Usings(string properties)
    {
        using var folder = new TempFolder();
        File.Copy(Path.Combine(Repo.Root, "global.json"), Path.Combine(folder.Path, "global.json"));
        var props = Path.Combine(Repo.Root, "src", "Winshell.Package", "buildTransitive", "Winshell.props");
        var project = Path.Combine(folder.Path, "Consumer.csproj");
        File.WriteAllText(project, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <Import Project="{props}" />
              <PropertyGroup>
                <TargetFramework>net11.0-windows10.0.22621.0</TargetFramework>
                {properties}
              </PropertyGroup>
            </Project>
            """);
        var result = Path.Combine(folder.Path, "usings.json");

        var evaluated = ShellProject.Run(
            "dotnet", ["msbuild", project, "-getItem:Using", $"-getResultOutputFile:{result}", "-nologo"], folder.Path);

        Assert.True(evaluated.ExitCode == 0, evaluated.Output);
        using var json = JsonDocument.Parse(File.ReadAllText(result));
        return json.RootElement.GetProperty("Items").GetProperty("Using").EnumerateArray()
            .Select(item => (item.TryGetProperty("Static", out var isStatic) && isStatic.GetString() == "true"
                ? "static "
                : "") + item.GetProperty("Identity").GetString())
            .ToList();
    }
}
