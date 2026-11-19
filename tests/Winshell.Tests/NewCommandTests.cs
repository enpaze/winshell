using System.Xml.Linq;
using Winshell.Commands;

namespace Winshell.Tests;

[TestClass]
public sealed partial class NewCommandTests : IDisposable
{
    private readonly TempFolder _root = new();

    private string ShellsRoot => Path.Combine(_root.Path, "config");

    private string Packages => Path.Combine(_root.Path, "packages");

    private static string Version => Shell.Version.Split('+')[0];

    [TestMethod]
    public void With_no_name_it_lists_the_starter_then_every_folder_under_examples()
    {
        var examples = Directory.EnumerateDirectories(Path.Combine(Repo.Root, "examples"))
            .Where(folder => Directory.EnumerateFiles(folder, "*.csproj").Any())
            .Select(Path.GetFileName)
            .Order(StringComparer.OrdinalIgnoreCase);

        var result = New();

        Assert.AreEqual(0, result.Code);
        var names = result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Split(' ')[0]);
        Assert.AreSequenceEqual(["starter", .. examples], names);
        var omarchy = result.Output.Split(Environment.NewLine)
            .Single(line => line.StartsWith("  omarchy-style ", StringComparison.Ordinal));
        Assert.EndsWith(" Omarchy-style panel", omarchy, StringComparison.Ordinal);
    }

    [TestMethod]
    public void An_example_lands_in_the_config_folder_referencing_this_releases_package_and_no_repo_path()
    {
        var result = New("omarchy-style");

        var folder = Path.Combine(ShellsRoot, "omarchy-style");
        Assert.AreEqual(0, result.Code);
        Assert.AreEqual(
            $"Created {folder}{Environment.NewLine}" +
            $"The Winshell {Version} package is not beside winshell.exe, so add a NuGet source for it: see https://github.com/enpaze/winshell/blob/main/docs/guide.md#from-a-clone{Environment.NewLine}" +
            $"Run it: winshell run -s omarchy-style{Environment.NewLine}",
            result.Output);
        var project = XDocument.Load(Path.Combine(folder, "OmarchyStyle.csproj")).Root!;
        Assert.IsEmpty(project.Elements("Import"));
        Assert.IsEmpty(project.Descendants("ProjectReference"));
        var package = Assert.ContainsSingle(project.Descendants("PackageReference"));
        Assert.AreEqual("Winshell", (string?)package.Attribute("Include"));
        Assert.AreEqual(Version, (string?)package.Attribute("Version"));
        Assert.DoesNotContain("..", File.ReadAllText(Path.Combine(folder, "OmarchyStyle.csproj")),
            StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(folder, "nuget.config")));
    }

    [TestMethod]
    public void Every_file_of_an_example_comes_along_byte_for_byte_except_its_project_and_readme()
    {
        var source = Path.Combine(Repo.Root, "examples", "omarchy-style");

        New("omarchy-style");

        var folder = Path.Combine(ShellsRoot, "omarchy-style");
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(source, file))
            .Where(file => !file.StartsWith(@"bin\", StringComparison.Ordinal) &&
                           !file.StartsWith(@"obj\", StringComparison.Ordinal))
            .ToList();
        Assert.AreSequenceEqual(files.Order(), Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(folder, file)).Order());
        foreach (var file in files.Where(file => file is not ("OmarchyStyle.csproj" or "README.md")))
            Assert.AreSequenceEqual(File.ReadAllBytes(Path.Combine(source, file)), File.ReadAllBytes(Path.Combine(folder, file)));
    }

    [TestMethod]
    public void A_readme_section_marked_repo_only_is_left_out_up_to_the_next_heading_as_high()
    {
        New("omarchy-style");

        var readme = File.ReadAllText(Path.Combine(ShellsRoot, "omarchy-style", "README.md"));
        Assert.DoesNotContain("## Run it", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("## Use it as your own shell", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("winshell run --path examples/omarchy-style", readme, StringComparison.Ordinal);
        Assert.DoesNotContain(Example.RepoOnly, readme, StringComparison.Ordinal);
        Assert.Contains("## What it shows", readme, StringComparison.Ordinal);
        Assert.Contains("## Credits", readme, StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_starter_gets_this_release_in_the_requested_folder()
    {
        var target = Path.Combine(_root.Path, "else where");

        var result = New("starter", "-p", target);

        Assert.AreEqual(0, result.Code);
        Assert.EndsWith($"Run it: winshell run -p \"{target}\"{Environment.NewLine}", result.Output,
            StringComparison.Ordinal);
        var package =
            Assert.ContainsSingle(XDocument.Load(Path.Combine(target, "MyShell.csproj")).Descendants("PackageReference"));
        Assert.AreEqual(Version, (string?)package.Attribute("Version"));
        Assert.IsFalse(Directory.Exists(ShellsRoot));
    }

    [TestMethod]
    public void With_this_releases_package_beside_winshell_the_copy_adds_that_folder_as_a_source()
    {
        Directory.CreateDirectory(Packages);
        File.WriteAllBytes(Path.Combine(Packages, $"Winshell.{Version}.nupkg"), []);

        var result = New("omarchy-style");

        Assert.DoesNotContain("not beside winshell.exe", result.Output, StringComparison.Ordinal);
        var config = XDocument.Load(Path.Combine(ShellsRoot, "omarchy-style", "nuget.config")).Root!;
        var source = Assert.ContainsSingle(config.Element("packageSources")!.Elements());
        Assert.AreEqual("add", source.Name.LocalName);
        Assert.AreEqual(Packages, (string?)source.Attribute("value"));
        Assert.IsEmpty(config.Descendants("clear"));
    }

    [TestMethod]
    public void Another_releases_package_beside_winshell_gives_no_nuget_config()
    {
        Directory.CreateDirectory(Packages);
        File.WriteAllBytes(Path.Combine(Packages, "Winshell.0.0.1.nupkg"), []);

        New("omarchy-style");

        Assert.IsFalse(File.Exists(Path.Combine(ShellsRoot, "omarchy-style", "nuget.config")));
    }

    [TestMethod]
    public void An_unknown_name_exits_2_and_writes_nothing()
    {
        var result = New("nope");

        Assert.AreEqual(
            new NewCommand.Result(2, "", "No starter or example is named \"nope\". Run winshell new to list them."),
            result);
        Assert.IsFalse(Directory.Exists(ShellsRoot));
    }

    [TestMethod]
    public void A_folder_that_is_not_empty_exits_2_and_is_left_as_it_was()
    {
        var folder = Path.Combine(ShellsRoot, "starter");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "mine.txt"), "mine");

        var result = New("starter");

        Assert.AreEqual(
            new NewCommand.Result(2, "", $"{folder} already exists and isn't empty. Pick another folder with -p."),
            result);
        Assert.AreSequenceEqual(["mine.txt"], Directory.EnumerateFileSystemEntries(folder).Select(Path.GetFileName));
        Assert.AreEqual("mine", File.ReadAllText(Path.Combine(folder, "mine.txt")));
    }

    [TestMethod]
    public void An_empty_folder_is_used()
    {
        var folder = Path.Combine(ShellsRoot, "starter");
        Directory.CreateDirectory(folder);

        Assert.AreEqual(0, New("starter").Code);
        Assert.IsTrue(File.Exists(Path.Combine(folder, "MyShell.csproj")));
    }

    public void Dispose()
    {
        _root.Dispose();
    }

    private NewCommand.Result New(params string[] words)
    {
        return NewCommand.Make(CliParser.Parse(["new", .. words]), ShellsRoot, Packages);
    }
}
