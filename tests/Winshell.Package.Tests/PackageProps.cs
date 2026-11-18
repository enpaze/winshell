using System.Text.Json;

namespace Winshell.Package.Tests;

internal static class PackageProps
{
    public static List<JsonElement> Items(string properties, string item, string? target = null)
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
        var result = Path.Combine(folder.Path, "items.json");
        List<string> arguments = ["msbuild", project, $"-getItem:{item}", $"-getResultOutputFile:{result}", "-nologo"];
        if (target is not null)
            arguments.Add($"-t:{target}");

        var evaluated = ShellProject.Run("dotnet", arguments, folder.Path);

        Assert.True(evaluated.ExitCode == 0, evaluated.Output);
        using var json = JsonDocument.Parse(File.ReadAllText(result));
        return json.RootElement.GetProperty("Items").GetProperty(item).EnumerateArray().Select(e => e.Clone()).ToList();
    }
}
