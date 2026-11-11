using System.Xml.Linq;
using Winshell.Commands;

namespace Winshell.Tests;

/// <summary>
/// <c>winshell new</c> copies the starter or an example from winshell.exe into a temp folder this class owns. The
/// packages folder beside winshell.exe is a temp folder too, so nothing here depends on how the CLI was built.
/// </summary>
public sealed partial class NewCommandTests : IDisposable
{
    private readonly TempFolder _root = new();

    private string ConfigRoot => Path.Combine(_root.Path, "config");

    private string Packages => Path.Combine(_root.Path, "packages");

    private static string Version => Shell.Version.Split('+')[0];

    [Fact]
    public void With_no_name_it_lists_the_starter_then_every_folder_under_examples()
    {
        var examples = Directory.EnumerateDirectories(Path.Combine(Repo.Root, "examples"))
            .Where(folder => Directory.EnumerateFiles(folder, "*.csproj").Any())
            .Select(Path.GetFileName)
            .Order(StringComparer.OrdinalIgnoreCase);

        var result = New();

        Assert.Equal(0, result.Code);
        var names = result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Split(' ')[0]);
        Assert.Equal(["starter", .. examples], names);
        var lines = result.Output.Split(Environment.NewLine);
        var starter = lines.Single(line => line.StartsWith("  starter ", StringComparison.Ordinal));
        Assert.EndsWith(" A bar with a clock and a battery", starter, StringComparison.Ordinal);
        var omarchy = lines.Single(line => line.StartsWith("  omarchy-style ", StringComparison.Ordinal));
        Assert.EndsWith(" Omarchy-style panel", omarchy, StringComparison.Ordinal);
    }

    [Fact]
    public void A_copy_writes_the_project_this_release_embedded_unchanged()
    {
        New("omarchy-style");
        var starter = Path.Combine(_root.Path, "starter");
        New("starter", "-p", starter);

        Assert.Equal(Embedded("new/omarchy-style/OmarchyStyle.csproj"),
            Read(Path.Combine(ConfigRoot, "omarchy-style", "OmarchyStyle.csproj")));
        Assert.Equal(Embedded("new/starter/MyShell.csproj"),
            Read(Path.Combine(starter, "MyShell.csproj")));
    }

    [Fact]
    public void An_example_lands_in_the_config_folder_referencing_this_releases_package_and_no_repo_path()
    {
        var result = New("omarchy-style");

        var folder = Path.Combine(ConfigRoot, "omarchy-style");
        Assert.Equal(0, result.Code);
        Assert.Equal(
            $"Created {folder}{Environment.NewLine}" +
            $"The Winshell {Version} package is not beside winshell.exe, so add a NuGet source for it: see https://github.com/enpaze/winshell/blob/main/docs/configuration.md#from-a-clone{Environment.NewLine}" +
            $"Run it: winshell run -c omarchy-style{Environment.NewLine}",
            result.Output);
        var project = XDocument.Load(Path.Combine(folder, "OmarchyStyle.csproj")).Root!;
        Assert.Empty(project.Elements("Import"));
        Assert.Empty(project.Descendants("ProjectReference"));
        var package = Assert.Single(project.Descendants("PackageReference"));
        Assert.Equal("Winshell", (string?)package.Attribute("Include"));
        Assert.Equal(Version, (string?)package.Attribute("Version"));
        Assert.DoesNotContain("..", File.ReadAllText(Path.Combine(folder, "OmarchyStyle.csproj")),
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(folder, "nuget.config")));
    }

    [Fact]
    public void Every_file_of_an_example_comes_along_byte_for_byte_except_its_project_and_readme()
    {
        var source = Path.Combine(Repo.Root, "examples", "omarchy-style");

        New("omarchy-style");

        var folder = Path.Combine(ConfigRoot, "omarchy-style");
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(source, file))
            .Where(file => !file.StartsWith(@"bin\", StringComparison.Ordinal) &&
                           !file.StartsWith(@"obj\", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(files.Order(), Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(folder, file)).Order());
        foreach (var file in files.Where(file => file is not ("OmarchyStyle.csproj" or "README.md")))
            Assert.Equal(File.ReadAllBytes(Path.Combine(source, file)), File.ReadAllBytes(Path.Combine(folder, file)));
    }

    [Fact]
    public void A_readme_section_marked_repo_only_is_left_out_up_to_the_next_heading_as_high()
    {
        New("omarchy-style");

        var readme = File.ReadAllText(Path.Combine(ConfigRoot, "omarchy-style", "README.md"));
        Assert.DoesNotContain("## Run it", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("## Use it as your own configuration", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("winshell run --path examples/omarchy-style", readme, StringComparison.Ordinal);
        Assert.DoesNotContain(Example.RepoOnly, readme, StringComparison.Ordinal);
        Assert.Contains("## What it shows", readme, StringComparison.Ordinal);
        Assert.Contains("## Credits", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void The_starter_gets_this_release_and_leaves_its_dotnet_new_settings_behind()
    {
        var target = Path.Combine(_root.Path, "else where");

        var result = New("starter", "-p", target);

        Assert.Equal(0, result.Code);
        Assert.EndsWith($"Run it: winshell run -p \"{target}\"{Environment.NewLine}", result.Output,
            StringComparison.Ordinal);
        var package =
            Assert.Single(XDocument.Load(Path.Combine(target, "MyShell.csproj")).Descendants("PackageReference"));
        Assert.Equal(Version, (string?)package.Attribute("Version"));
        Assert.False(Directory.Exists(Path.Combine(target, ".template.config")));
        Assert.False(Directory.Exists(ConfigRoot));
    }

    [Fact]
    public void With_this_releases_package_beside_winshell_the_copy_adds_that_folder_as_a_source()
    {
        Directory.CreateDirectory(Packages);
        File.WriteAllBytes(Path.Combine(Packages, $"Winshell.{Version}.nupkg"), []);

        var result = New("omarchy-style");

        Assert.DoesNotContain("not beside winshell.exe", result.Output, StringComparison.Ordinal);
        var config = XDocument.Load(Path.Combine(ConfigRoot, "omarchy-style", "nuget.config")).Root!;
        var source = Assert.Single(config.Element("packageSources")!.Elements());
        Assert.Equal("add", source.Name.LocalName);
        Assert.Equal(Packages, (string?)source.Attribute("value"));
        Assert.Empty(config.Descendants("clear"));
    }

    [Fact]
    public void Another_releases_package_beside_winshell_gives_no_nuget_config()
    {
        Directory.CreateDirectory(Packages);
        File.WriteAllBytes(Path.Combine(Packages, "Winshell.0.0.1.nupkg"), []);

        New("omarchy-style");

        Assert.False(File.Exists(Path.Combine(ConfigRoot, "omarchy-style", "nuget.config")));
    }

    [Fact]
    public void An_unknown_name_exits_2_and_writes_nothing()
    {
        var result = New("nope");

        Assert.Equal(
            new NewCommand.Result(2, "", "No starter or example is named \"nope\". Run winshell new to list them."),
            result);
        Assert.False(Directory.Exists(ConfigRoot));
    }

    [Fact]
    public void A_folder_that_is_not_empty_exits_2_and_is_left_as_it_was()
    {
        var folder = Path.Combine(ConfigRoot, "starter");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "mine.txt"), "mine");

        var result = New("starter");

        Assert.Equal(
            new NewCommand.Result(2, "", $"{folder} already exists and isn't empty. Pick another folder with -p."),
            result);
        Assert.Equal(["mine.txt"], Directory.EnumerateFileSystemEntries(folder).Select(Path.GetFileName));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(folder, "mine.txt")));
    }

    [Fact]
    public void An_empty_folder_is_used()
    {
        var folder = Path.Combine(ConfigRoot, "starter");
        Directory.CreateDirectory(folder);

        Assert.Equal(0, New("starter").Code);
        Assert.True(File.Exists(Path.Combine(folder, "MyShell.csproj")));
    }

    public void Dispose()
    {
        _root.Dispose();
    }

    private NewCommand.Result New(params string[] words)
    {
        return NewCommand.Make(CliParser.Parse(["new", .. words]), ConfigRoot, Packages);
    }

    /// <summary>The project text winshell.exe carries for <paramref name="path"/>, with newlines folded to <c>\n</c>.</summary>
    private static string Embedded(string path)
    {
        var resource = typeof(Example).Assembly.GetManifestResourceNames()
            .Single(name => name.Replace('\\', '/').Equals(path, StringComparison.Ordinal));
        using var stream = typeof(Example).Assembly.GetManifestResourceStream(resource);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string Read(string path)
    {
        return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
