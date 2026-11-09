using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Winshell.Commands;

namespace Winshell.Tests;

/// <summary>
/// <c>winshell widget</c> against a repository on this machine. The GitHub form is parsed and never contacted.
/// </summary>
public sealed class WidgetCommandTests : IDisposable
{
    private const string Warning =
        "Building runs this repository's code and MSBuild targets in a program that starts at sign-in.";

    private const string NeedsSdk = "Building a configuration needs the .NET SDK, from https://dot.net/download.";

    private readonly TempFolder _root = new();

    private string Root => _root.Path;

    private string Local => Path.Combine(Root, "local");

    private string ConfigDir => Path.Combine(Root, "shell");

    private string ProjectFile => Path.Combine(ConfigDir, "Shell.csproj");

    private string Props => Path.Combine(ConfigDir, "widgets.props");

    private string ProgramFile => Path.Combine(ConfigDir, "bin", "shell", "Shell.exe");

    private ShellLocations Locations => new(Path.Combine(Root, "configs"), Local);

    public WidgetCommandTests()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(Local);
        File.Copy(Path.Combine(Repo.Root, "global.json"), Path.Combine(Root, "global.json"));
        File.WriteAllText(Path.Combine(Root, "nuget.config"), """
                                                              <?xml version="1.0" encoding="utf-8"?>
                                                              <configuration>
                                                                <packageSources>
                                                                  <clear />
                                                                </packageSources>
                                                              </configuration>
                                                              """);
        WriteConfig();
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
        }

        _root.Dispose();
    }

    [Fact]
    public void Installing_a_tagged_commit_pins_it_and_lists_it()
    {
        var program = PlantProgram();
        var repo = Commit("clocks", Clock("FIRST"), Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "first");
        Git(repo, "tag", "v1");
        var tagged = Rev(repo, "v1");
        File.WriteAllText(Path.Combine(repo, "Clock.csproj"),
            Library("""<WinshellWidget Include="other" Type="ClockWidget.Other" />"""));
        File.WriteAllText(Path.Combine(repo, "Clock.cs"), Clock("SECOND"));
        CommitExisting(repo, "second");
        var head = Rev(repo, "HEAD");
        var url = new Uri(repo).AbsoluteUri;

        var run = Run(["install", url + "@v1"], input: "n", yes: true);

        Assert.NotEqual(tagged, head);
        Assert.Equal(0, run.Code);
        Assert.Null(run.Error);
        Assert.Equal($"{url}{NewLine}Commit {tagged}{NewLine}Widgets: clock (ClockWidget.Clock){NewLine}{Warning}{NewLine}" +
                     $"Installed clock from {url} at {tagged}.{NewLine}", run.Output);
        var reference = Assert.Single(XDocument.Load(Props).Descendants("ProjectReference"));
        var widget = Assert.Single(reference.Parent!.Elements("WinshellWidget"));
        Assert.Equal(url, Value(reference, "WinshellSource"));
        Assert.Equal("v1", Value(reference, "WinshellRef"));
        Assert.Equal(tagged, Value(reference, "WinshellCommit"));
        Assert.Equal("WinshellWidgetsFile", Value(reference, "UndefineProperties"));
        Assert.Contains(tagged, (string)reference.Attribute("Include")!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("clock", widget.Attribute("Include")!.Value);
        Assert.Equal("ClockWidget.Clock", widget.Attribute("Type")!.Value);
        Assert.Equal(url, Value(widget, "WinshellSource"));
        Assert.True(reference.IsBefore(widget));
        var copy = Assert.Single(Directory.GetFiles(Path.Combine(Local, "widgets"), "Clock.cs", SearchOption.AllDirectories));
        var library = Path.GetDirectoryName(copy)!;
        // The staged build compiles this library. The global widgets file would make it import that file and reference
        // itself; the reference clears the property, so the library keeps the default beside itself and references nothing.
        var seenFile = File.ReadAllText(Path.Combine(library, "seen-widgets.txt")).Trim();
        var seenReferences = File.ReadAllText(Path.Combine(library, "seen-references.txt")).Trim();
        Assert.DoesNotContain("widget-build", seenFile, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine(library, "widgets.props"), seenFile, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", seenReferences);
        Assert.Contains(Path.Combine("widgets", "local"), copy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(tagged, copy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FIRST", File.ReadAllText(copy), StringComparison.Ordinal);
        Assert.DoesNotContain("SECOND", File.ReadAllText(copy), StringComparison.Ordinal);
        Assert.True(File.GetAttributes(copy).HasFlag(FileAttributes.ReadOnly));
        Assert.True(File.GetAttributes(Path.Combine(Path.GetDirectoryName(copy)!, "Clock.csproj"))
            .HasFlag(FileAttributes.ReadOnly));
        Assert.Equal(program, File.ReadAllBytes(ProgramFile));
        var builds = Path.Combine(Local, "cache", "widget-build");
        Assert.True(!Directory.Exists(builds) || Directory.GetDirectories(builds).Length == 0);
        var listed = Run(["list"]);
        Assert.Equal($"clock{NewLine}  source: {url}{NewLine}  ref: v1{NewLine}  commit: {tagged}{NewLine}", listed.Output);
        var props = File.ReadAllBytes(Props);

        var again = Run(["install", url], input: "y");

        Assert.Equal(1, again.Code);
        Assert.Contains($"{url} is already installed at {tagged}.", again.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(head, again.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Install?", again.Output, StringComparison.Ordinal);
        Assert.Equal(props, File.ReadAllBytes(Props));
    }

    [Fact]
    public void A_failed_build_leaves_the_props_file_and_the_program()
    {
        var program = PlantProgram();
        File.WriteAllText(Props, """
                                 <Project>
                                   <ItemGroup>
                                     <ProjectReference Include="kept.csproj">
                                       <WinshellSource>kept-source</WinshellSource>
                                       <WinshellRef>kept-ref</WinshellRef>
                                       <WinshellCommit>aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</WinshellCommit>
                                     </ProjectReference>
                                     <WinshellWidget Include="kept" Type="Kept.Widget">
                                       <WinshellSource>kept-source</WinshellSource>
                                     </WinshellWidget>
                                   </ItemGroup>
                                 </Project>
                                 """);
        var props = File.ReadAllBytes(Props);
        var repo = Commit("broken", Broken, Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "broken");

        var run = Run(["install", repo], yes: true);

        Assert.Equal(1, run.Code);
        Assert.Contains("error CS", run.Error, StringComparison.Ordinal);
        Assert.Equal(props, File.ReadAllBytes(Props));
        Assert.Equal(program, File.ReadAllBytes(ProgramFile));
    }

    [Fact]
    public void A_newer_Winshell_requirement_stops_before_building()
    {
        WriteConfig("0.6.0");
        var program = PlantProgram();
        var repo = Commit("newer", Broken,
            Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />""", "9.0.0"), "newer");

        var run = Run(["install", repo], input: "y");

        Assert.Equal(1, run.Code);
        Assert.Equal("This widget needs Winshell 9.0.0 and this configuration uses 0.6.0.", run.Error);
        Assert.DoesNotContain("Install?", run.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("error CS", run.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(Props));
        Assert.Equal(program, File.ReadAllBytes(ProgramFile));
    }

    [Fact]
    public void A_Winshell_package_reference_restores_from_the_configurations_source()
    {
        // The package exists only in the folder the configuration's nuget.config names. That file is not a parent of
        // the clone. The version is exact: an unbracketed version is a minimum, and a newer Winshell on the machine
        // would satisfy it.
        var packages = Path.Combine(Root, "packages");
        WritePackage(packages, "Winshell", "0.1.0");
        File.WriteAllText(Path.Combine(ConfigDir, "nuget.config"), $"""
                                                                     <?xml version="1.0" encoding="utf-8"?>
                                                                     <configuration>
                                                                       <packageSources>
                                                                         <add key="winshell" value="{packages}" />
                                                                       </packageSources>
                                                                     </configuration>
                                                                     """);
        var repo = Commit("packaged", Clock("FIRST"),
            Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />""", "[0.1.0]"), "packaged");

        var run = Run(["install", repo], yes: true);

        Assert.Null(run.Error);
        Assert.Equal(0, run.Code);
        Assert.Contains("Installed clock", run.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(Props));
    }

    [Fact]
    public void Declining_the_question_changes_nothing()
    {
        var program = PlantProgram();
        var repo = Commit("asked", Clock("FIRST"), Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "asked");
        var commit = Rev(repo, "HEAD");

        var run = Run(["install", repo], input: "n");

        Assert.Equal(0, run.Code);
        Assert.Null(run.Error);
        Assert.Contains(repo, run.Output, StringComparison.Ordinal);
        Assert.Contains($"Commit {commit}", run.Output, StringComparison.Ordinal);
        Assert.Contains("clock (ClockWidget.Clock)", run.Output, StringComparison.Ordinal);
        Assert.Contains(Warning, run.Output, StringComparison.Ordinal);
        Assert.Contains("Install? [y/N] ", run.Output, StringComparison.Ordinal);
        Assert.Contains("Nothing was installed.", run.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(Props));
        Assert.Equal(program, File.ReadAllBytes(ProgramFile));
    }

    [Fact]
    public void Remove_drops_every_widget_from_that_source()
    {
        var repo = Commit("both", Clock("FIRST"), Library("""
                                                          <WinshellWidget Include="clock" Type="ClockWidget.Clock" />
                                                          <WinshellWidget Include="date" Type="ClockWidget.Date" />
                                                          """), "both");
        var installed = Run(["install", repo], yes: true);
        Assert.Equal(0, installed.Code);
        Assert.Equal("master", Value(XDocument.Load(Props).Descendants("ProjectReference").Single(), "WinshellRef"));
        Assert.Equal(Rev(repo, "HEAD"), Value(XDocument.Load(Props).Descendants("ProjectReference").Single(), "WinshellCommit"));
        var written = File.ReadAllBytes(Props);

        var byName = Run(["remove", "date"]);

        Assert.Equal(0, byName.Code);
        Assert.Equal($"Removed {repo}.{NewLine}", byName.Output);
        Assert.False(File.Exists(Props));
        Assert.Equal($"No widgets are installed.{NewLine}", Run(["list"]).Output);
        File.WriteAllBytes(Props, written);

        var bySource = Run(["remove", Path.GetFileName(repo)]);

        Assert.Equal(0, bySource.Code);
        Assert.Equal($"Removed {repo}.{NewLine}", bySource.Output);
        Assert.False(File.Exists(Props));
    }

    [Fact]
    public void A_widget_name_from_another_source_is_refused()
    {
        var first = Commit("one", Clock("FIRST"), Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "one");
        var second = Commit("two", Clock("SECOND"), Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "two");
        Assert.Equal(0, Run(["install", first], yes: true).Code);
        var props = File.ReadAllBytes(Props);

        var run = Run(["install", second], input: "y");

        Assert.Equal(1, run.Code);
        Assert.Equal($"clock is already installed from {first}. {second} also provides it.", run.Error);
        Assert.DoesNotContain("Install?", run.Output, StringComparison.Ordinal);
        Assert.Equal(props, File.ReadAllBytes(Props));
    }

    [Fact]
    public void Several_widget_projects_are_named_instead_of_installed()
    {
        var repo = Path.Combine(Root, "multi");
        Directory.CreateDirectory(Path.Combine(repo, "a"));
        Directory.CreateDirectory(Path.Combine(repo, "b"));
        File.WriteAllText(Path.Combine(repo, "a", "One.csproj"),
            Library("""<WinshellWidget Include="one" Type="One.Widget" />"""));
        File.WriteAllText(Path.Combine(repo, "b", "Two.csproj"),
            Library("""<WinshellWidget Include="two" Type="Two.Widget" />"""));
        File.WriteAllText(Path.Combine(repo, "a", "One.cs"), Clock("FIRST"));
        Git(repo, "init", "-b", "master");
        CommitExisting(repo, "two projects");

        var run = Run(["install", repo], input: "y");

        Assert.Equal(1, run.Code);
        Assert.Contains("One.csproj", run.Error, StringComparison.Ordinal);
        Assert.Contains("Two.csproj", run.Error, StringComparison.Ordinal);
        Assert.Contains("--project", run.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Install?", run.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(Props));
    }

    [Fact]
    public void Installing_needs_git_and_the_dotnet_sdk()
    {
        var noGit = Run(["install", "github.com/someone/clock-widget"], git: Path.Combine(Root, "missing-git.exe"));

        Assert.Equal(1, noGit.Code);
        Assert.Equal("Installing a widget needs git, from https://git-scm.com/download/win.", noGit.Error);
        Assert.False(Directory.Exists(Path.Combine(Local, "widgets")));
        Assert.False(File.Exists(Props));

        var repo = Commit("present", Clock("FIRST"), Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "present");
        var noSdk = Run(["install", repo], dotnet: Path.Combine(Root, "missing-dotnet.exe"));

        Assert.Equal(1, noSdk.Code);
        Assert.Equal(NeedsSdk, noSdk.Error);
        Assert.False(Directory.Exists(Path.Combine(Local, "widgets")));
        Assert.False(File.Exists(Props));
    }

    [Theory]
    [InlineData("github.com/someone/clock-widget", "github.com/someone/clock-widget",
        "https://github.com/someone/clock-widget.git", null)]
    [InlineData("github.com/someone/widgets@v1.2", "github.com/someone/widgets",
        "https://github.com/someone/widgets.git", "v1.2")]
    [InlineData("https://github.com/someone/widgets.git@v1.2", "github.com/someone/widgets",
        "https://github.com/someone/widgets.git", "v1.2")]
    public void A_GitHub_repository_is_accepted_without_contacting_it(string text, string canonical, string gitUrl,
        string? reference)
    {
        Assert.True(WidgetCommand.TryParse(text, out var source, out var error));

        Assert.Null(error);
        Assert.Equal(canonical, source.Canonical);
        Assert.Equal(gitUrl, source.GitUrl);
        Assert.Equal(reference, source.Ref);
    }

    [Fact]
    public void A_source_that_is_not_a_repository_is_refused()
    {
        var run = Run(["install", "not-a-source"]);

        Assert.Equal(2, run.Code);
        Assert.Contains("github.com/owner/repo", run.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(Props));
    }

    [Fact]
    public void Update_moves_to_the_new_commit_and_shows_its_log()
    {
        var repo = Commit("clocks", Clock("FIRST"), Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "first");
        Assert.Equal(0, Run(["install", repo], yes: true).Code);
        var old = Rev(repo, "HEAD");
        var oldCopy = ClockFile(old);
        File.WriteAllText(Path.Combine(repo, "Clock.cs"), Clock("SECOND"));
        File.WriteAllText(Path.Combine(repo, "Clock.csproj"), Library("""
                                                                      <WinshellWidget Include="clock" Type="ClockWidget.Clock" />
                                                                      <WinshellWidget Include="date" Type="ClockWidget.Date" />
                                                                      """));
        CommitExisting(repo, "second adds date");
        File.WriteAllText(Path.Combine(repo, "Clock.cs"), Clock("THIRD"));
        File.WriteAllText(Path.Combine(repo, "Clock.csproj"),
            Library("""<WinshellWidget Include="date" Type="ClockWidget.Date" />"""));
        CommitExisting(repo, "third drops clock");
        var head = Rev(repo, "HEAD");

        var run = Run(["update"], yes: true);

        Assert.Equal(0, run.Code);
        Assert.Null(run.Error);
        Assert.Contains(repo, run.Output, StringComparison.Ordinal);
        Assert.Contains($"{old}..{head}", run.Output, StringComparison.Ordinal);
        Assert.Contains("second adds date", run.Output, StringComparison.Ordinal);
        Assert.Contains("third drops clock", run.Output, StringComparison.Ordinal);
        Assert.Contains("Added: date", run.Output, StringComparison.Ordinal);
        Assert.Contains("Dropped: clock", run.Output, StringComparison.Ordinal);
        Assert.Contains(Warning, run.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Update?", run.Output, StringComparison.Ordinal);
        Assert.Contains($"Updated date from {repo} at {head}.", run.Output, StringComparison.Ordinal);
        var reference = Assert.Single(XDocument.Load(Props).Descendants("ProjectReference"));
        var widget = Assert.Single(reference.Parent!.Elements("WinshellWidget"));
        Assert.Equal(repo, Value(reference, "WinshellSource"));
        Assert.Equal("master", Value(reference, "WinshellRef"));
        Assert.Equal(head, Value(reference, "WinshellCommit"));
        Assert.Equal("WinshellWidgetsFile", Value(reference, "UndefineProperties"));
        Assert.Contains(head, (string)reference.Attribute("Include")!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("date", widget.Attribute("Include")!.Value);
        Assert.Equal("ClockWidget.Date", widget.Attribute("Type")!.Value);
        Assert.Contains("FIRST", File.ReadAllText(oldCopy), StringComparison.Ordinal);
        Assert.True(File.GetAttributes(oldCopy).HasFlag(FileAttributes.ReadOnly));
        var updated = ClockFile(head);
        Assert.NotEqual(oldCopy, updated);
        Assert.Contains("THIRD", File.ReadAllText(updated), StringComparison.Ordinal);
        Assert.DoesNotContain("SECOND", File.ReadAllText(updated), StringComparison.Ordinal);
        Assert.True(File.GetAttributes(updated).HasFlag(FileAttributes.ReadOnly));
    }

    [Fact]
    public void An_unchanged_ref_is_up_to_date()
    {
        var repo = Commit("tagged", Clock("FIRST"), Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "first");
        Git(repo, "tag", "v1");
        var tagged = Rev(repo, "v1");
        Assert.Equal(0, Run(["install", repo + "@v1"], yes: true).Code);
        File.WriteAllText(Path.Combine(repo, "Clock.cs"), Clock("SECOND"));
        CommitExisting(repo, "second");
        var props = File.ReadAllBytes(Props);

        var run = Run(["update"], yes: true);

        Assert.Equal(0, run.Code);
        Assert.Null(run.Error);
        Assert.Equal($"{repo} is up to date at {tagged}.{NewLine}", run.Output);
        Assert.DoesNotContain("second", run.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Update?", run.Output, StringComparison.Ordinal);
        Assert.Equal(props, File.ReadAllBytes(Props));
        var copy = Assert.Single(Directory.GetFiles(Path.Combine(Local, "widgets"), "Clock.cs", SearchOption.AllDirectories));
        Assert.Contains(tagged, copy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FIRST", File.ReadAllText(copy), StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_update_keeps_the_old_commit()
    {
        var program = PlantProgram();
        var repo = Commit("breaks", Clock("FIRST"), Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "first");
        Assert.Equal(0, Run(["install", repo], yes: true).Code);
        var old = Rev(repo, "HEAD");
        var props = File.ReadAllBytes(Props);
        File.WriteAllText(Path.Combine(repo, "Clock.cs"), Broken);
        CommitExisting(repo, "breaks the build");

        var run = Run(["update"], yes: true);

        Assert.Equal(1, run.Code);
        Assert.Contains("error CS", run.Error, StringComparison.Ordinal);
        Assert.Contains($"{old}..", run.Output, StringComparison.Ordinal);
        Assert.Contains("breaks the build", run.Output, StringComparison.Ordinal);
        Assert.Equal(props, File.ReadAllBytes(Props));
        Assert.Equal(old, Value(XDocument.Load(Props).Descendants("ProjectReference").Single(), "WinshellCommit"));
        Assert.Equal(program, File.ReadAllBytes(ProgramFile));
        Assert.Contains("FIRST", File.ReadAllText(ClockFile(old)), StringComparison.Ordinal);
    }

    [Fact]
    public void Declining_an_update_changes_nothing()
    {
        var program = PlantProgram();
        var repo = Commit("asked", Clock("FIRST"), Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "first");
        Assert.Equal(0, Run(["install", repo], yes: true).Code);
        var old = Rev(repo, "HEAD");
        var props = File.ReadAllBytes(Props);
        File.WriteAllText(Path.Combine(repo, "Clock.cs"), Clock("SECOND"));
        CommitExisting(repo, "second changes the clock");
        var head = Rev(repo, "HEAD");

        var run = Run(["update", "clock"], input: "n");

        Assert.Equal(0, run.Code);
        Assert.Null(run.Error);
        Assert.Contains($"{old}..{head}", run.Output, StringComparison.Ordinal);
        Assert.Contains("second changes the clock", run.Output, StringComparison.Ordinal);
        Assert.Contains("Added: none", run.Output, StringComparison.Ordinal);
        Assert.Contains("Dropped: none", run.Output, StringComparison.Ordinal);
        Assert.Contains(Warning, run.Output, StringComparison.Ordinal);
        Assert.Contains("Update? [y/N] ", run.Output, StringComparison.Ordinal);
        Assert.Contains("Nothing was updated.", run.Output, StringComparison.Ordinal);
        Assert.Equal(props, File.ReadAllBytes(Props));
        Assert.Equal(program, File.ReadAllBytes(ProgramFile));
    }

    [Fact]
    public void A_newer_Winshell_requirement_stops_an_update_before_building()
    {
        var repo = Commit("newer", Clock("FIRST"), Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />"""),
            "first");
        Assert.Equal(0, Run(["install", repo], yes: true).Code);
        var props = File.ReadAllBytes(Props);
        WriteConfig("0.6.0");
        File.WriteAllText(Path.Combine(repo, "Clock.csproj"),
            Library("""<WinshellWidget Include="clock" Type="ClockWidget.Clock" />""", "9.0.0"));
        CommitExisting(repo, "needs a newer winshell");

        var run = Run(["update"], input: "y");

        Assert.Equal(1, run.Code);
        Assert.Equal("This widget needs Winshell 9.0.0 and this configuration uses 0.6.0.", run.Error);
        Assert.DoesNotContain("Update?", run.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("needs a newer winshell", run.Output, StringComparison.Ordinal);
        Assert.Equal(props, File.ReadAllBytes(Props));
    }

    [Fact]
    public void Update_with_nothing_installed_says_so()
    {
        var run = Run(["update"]);

        Assert.Equal(0, run.Code);
        Assert.Null(run.Error);
        Assert.Equal($"No widgets are installed.{NewLine}", run.Output);
        Assert.False(File.Exists(Props));
    }

    [Fact]
    public void Update_refuses_a_name_that_is_not_installed()
    {
        File.WriteAllText(Props, """
                                 <Project>
                                   <ItemGroup>
                                     <ProjectReference Include="kept.csproj">
                                       <WinshellSource>kept-source</WinshellSource>
                                       <WinshellRef>kept-ref</WinshellRef>
                                       <WinshellCommit>aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</WinshellCommit>
                                     </ProjectReference>
                                     <WinshellWidget Include="kept" Type="Kept.Widget">
                                       <WinshellSource>kept-source</WinshellSource>
                                     </WinshellWidget>
                                   </ItemGroup>
                                 </Project>
                                 """);
        var props = File.ReadAllBytes(Props);

        var run = Run(["update", "missing"]);

        Assert.Equal(1, run.Code);
        Assert.Equal("No installed widget or source is named \"missing\".", run.Error);
        Assert.Equal(props, File.ReadAllBytes(Props));
    }

    [Fact]
    public void Updating_needs_git()
    {
        File.WriteAllText(Props, """
                                 <Project>
                                   <ItemGroup>
                                     <WinshellWidget Include="kept" Type="Kept.Widget">
                                       <WinshellSource>kept-source</WinshellSource>
                                     </WinshellWidget>
                                   </ItemGroup>
                                 </Project>
                                 """);
        var props = File.ReadAllBytes(Props);

        var run = Run(["update"], git: Path.Combine(Root, "missing-git.exe"));

        Assert.Equal(1, run.Code);
        Assert.Equal("Updating a widget needs git, from https://git-scm.com/download/win.", run.Error);
        Assert.Equal(props, File.ReadAllBytes(Props));
    }

    [Fact]
    public void Help_describes_install_list_remove_and_update()
    {
        var missing = Assert.Throws<ArgumentException>(() => CliParser.Parse(["widget"]));
        var yes = Assert.Throws<ArgumentException>(() => CliParser.Parse(["widget", "list", "--yes"]));
        var project = Assert.Throws<ArgumentException>(() => CliParser.Parse(["widget", "remove", "clock", "--project", "X"]));
        var projectOnUpdate = Assert.Throws<ArgumentException>(() => CliParser.Parse(["widget", "update", "--project", "X"]));
        var spare = Assert.Throws<ArgumentException>(() => CliParser.Parse(["widget", "update", "clock", "extra"]));
        var named = CliParser.Parse(["widget", "update", "clock", "--yes", "-c", "myshell"]);
        var summary = CliParser.Help(null);
        var help = CliParser.Help("widget");

        Assert.Equal("winshell widget needs a command. Run winshell widget --help.", missing.Message);
        Assert.Equal("winshell widget list does not take \"--yes\".", yes.Message);
        Assert.Equal("winshell widget remove does not take \"--project\".", project.Message);
        Assert.Equal("winshell widget update does not take \"--project\".", projectOnUpdate.Message);
        Assert.Equal("Unexpected argument \"extra\".", spare.Message);
        Assert.Equal("update", named.Positional[0]);
        Assert.Equal("clock", named.Positional[1]);
        Assert.True(named.Yes);
        Assert.Equal("myshell", named.Config);
        Assert.Contains("winshell widget install <source>[@ref] [--yes]", summary, StringComparison.Ordinal);
        Assert.Contains("winshell widget list", summary, StringComparison.Ordinal);
        Assert.Contains("list the widgets a configuration has", summary, StringComparison.Ordinal);
        Assert.Contains("winshell widget remove <name>", summary, StringComparison.Ordinal);
        Assert.Contains("winshell widget update [<name>] [--yes]", summary, StringComparison.Ordinal);
        Assert.Contains("move installed widgets to the ref's new commit", summary, StringComparison.Ordinal);
        Assert.Contains("update [<name>] [--yes]", help, StringComparison.Ordinal);
        Assert.Contains("up to date", help, StringComparison.Ordinal);
        Assert.Contains(Warning, help, StringComparison.Ordinal);
        Assert.Contains("--project", help, StringComparison.Ordinal);
        Assert.Contains("--yes", help, StringComparison.Ordinal);
        Assert.Contains("github.com/owner/repo", help, StringComparison.Ordinal);
    }

    private static string NewLine => Environment.NewLine;

    private void WriteConfig(string? winshellVersion = null)
    {
        var package = winshellVersion is null
            ? ""
            : $"""<ItemGroup><PackageReference Include="Winshell" Version="{winshellVersion}" /></ItemGroup>""";
        File.WriteAllText(ProjectFile, $"""
                                         <Project Sdk="Microsoft.NET.Sdk">
                                           <PropertyGroup>
                                             <OutputType>Exe</OutputType>
                                             <TargetFramework>net10.0</TargetFramework>
                                             <ImplicitUsings>enable</ImplicitUsings>
                                             <Nullable>enable</Nullable>
                                             <NuGetAudit>false</NuGetAudit>
                                             <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
                                           </PropertyGroup>
                                           <Import Project="$(WinshellWidgetsFile)" Condition="'$(WinshellWidgetsFile)' != '' and Exists('$(WinshellWidgetsFile)')" />
                                           {package}
                                         </Project>
                                         """);
        File.WriteAllText(Path.Combine(ConfigDir, "Program.cs"), """Console.WriteLine("shell");""");
    }

    private static string Library(string widgets, string? version = null)
    {
        var package = version is null
            ? ""
            : $"""<PackageReference Include="Winshell" Version="{version}" />""";
        return $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <NuGetAudit>false</NuGetAudit>
                    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
                  </PropertyGroup>
                  <PropertyGroup>
                    <WinshellWidgetsFile Condition="'$(WinshellWidgetsFile)' == ''">$(MSBuildProjectDirectory)\widgets.props</WinshellWidgetsFile>
                  </PropertyGroup>
                  <Import Project="$(WinshellWidgetsFile)" Condition="Exists('$(WinshellWidgetsFile)')" />
                  <ItemGroup>
                    {package}
                    {widgets}
                  </ItemGroup>
                  <Target Name="RecordWidgetReferences" BeforeTargets="CoreCompile">
                    <WriteLinesToFile File="$(MSBuildProjectDirectory)\seen-widgets.txt" Lines="$(WinshellWidgetsFile)" Overwrite="true" />
                    <WriteLinesToFile File="$(MSBuildProjectDirectory)\seen-references.txt" Lines="@(ProjectReference)" Overwrite="true" />
                  </Target>
                </Project>
                """;
    }

    private static string Clock(string marker)
    {
        return $$"""
                 namespace ClockWidget;

                 public sealed class Clock
                 {
                     public Clock(object context)
                     {
                         _ = context;
                         _ = "{{marker}}";
                     }
                 }
                 """;
    }

    private const string Broken = """
                                  namespace ClockWidget;

                                  public sealed class Clock
                                  {
                                      public Clock(object context)
                                      {
                                          this is not valid
                                      }
                                  }
                                  """;

    private byte[] PlantProgram()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ProgramFile)!);
        var bytes = "program-v1"u8.ToArray();
        File.WriteAllBytes(ProgramFile, bytes);
        return bytes;
    }

    private string Commit(string folder, string code, string project, string message)
    {
        var repo = Path.Combine(Root, folder);
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Clock.csproj"), project);
        File.WriteAllText(Path.Combine(repo, "Clock.cs"), code);
        Git(repo, "init", "-b", "master");
        CommitExisting(repo, message);
        return repo;
    }

    private static void CommitExisting(string repo, string message)
    {
        Git(repo, "add", "-A");
        Git(repo, "-c", "user.name=Widget Test", "-c", "user.email=widget@example.com", "-c", "commit.gpgsign=false",
            "commit", "-m", message);
    }

    private static string Rev(string repo, string revision)
    {
        return GitOut(repo, "rev-parse", revision).Trim();
    }

    private string ClockFile(string commit)
    {
        return Directory.GetFiles(Path.Combine(Local, "widgets"), "Clock.cs", SearchOption.AllDirectories)
            .Single(file => file.Contains(commit, StringComparison.OrdinalIgnoreCase));
    }

    private static string Value(XElement element, string name)
    {
        return element.Elements(name).Single().Value;
    }

    private Outcome Run(string[] positional, string input = "", bool yes = false, string? project = null, string? git = null,
        string? dotnet = null)
    {
        var output = new StringWriter();
        var result = WidgetCommand.Apply(new CliOptions
        {
            Command = "widget",
            Path = ProjectFile,
            Positional = [.. positional],
            Yes = yes,
            Project = project
        }, Locations, new StringReader(input), output, git ?? "git", dotnet ?? "dotnet");
        return new Outcome(result.Code, result.Error, output.ToString());
    }

    private readonly record struct Outcome(int Code, string? Error, string Output);

    /// <summary>A package with no assemblies. Restore can find it; the widget code does not use it.</summary>
    private static void WritePackage(string folder, string id, string version)
    {
        Directory.CreateDirectory(folder);
        var nuspec = $"""
                      <?xml version="1.0" encoding="utf-8"?>
                      <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                        <metadata>
                          <id>{id}</id>
                          <version>{version}</version>
                          <authors>Winshell</authors>
                          <description>A package the widget restore can find only through the configuration.</description>
                        </metadata>
                      </package>
                      """;
        var contentTypes = """
                           <?xml version="1.0" encoding="utf-8"?>
                           <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                             <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
                             <Default Extension="nuspec" ContentType="application/octet" />
                             <Default Extension="_" ContentType="application/octet" />
                           </Types>
                           """;
        var rels = $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Type="http://schemas.microsoft.com/packaging/2010/07/manifest" Target="/{id}.nuspec" Id="nuspec" />
                    </Relationships>
                    """;
        using var zip = ZipFile.Open(Path.Combine(folder, $"{id}.{version}.nupkg"), ZipArchiveMode.Create);
        WriteEntry(zip, $"{id}.nuspec", nuspec);
        WriteEntry(zip, "[Content_Types].xml", contentTypes);
        WriteEntry(zip, "_rels/.rels", rels);
        WriteEntry(zip, "lib/net10.0/_._", "");
    }

    private static void WriteEntry(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    private static void Git(string repo, params string[] args)
    {
        _ = GitOut(repo, args);
    }

    private static string GitOut(string repo, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = repo,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                error.AppendLine(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} exited {process.ExitCode}: {error}{output}");
        return output.ToString();
    }
}
