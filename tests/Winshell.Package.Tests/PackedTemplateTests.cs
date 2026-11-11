using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Winshell.Package.Tests;

/// <summary>
/// Packs the library and the template, builds a shell from it the way a start does, and runs it. This is slow, and the main
/// test stage leaves <c>Category=Package</c> out.
/// </summary>
[Trait("Category", "Package")]
[Collection(nameof(PackedTemplateTests))]
[CollectionDefinition(nameof(PackedTemplateTests), DisableParallelization = true)]
public sealed class PackedTemplateTests(PackedTemplates templates) : IClassFixture<PackedTemplates>
{
    private const int PatienceMs = 1_800_000;

    [Fact(Timeout = PatienceMs)]
    public void The_starter_shows_its_clock_and_battery_and_is_listed_and_killed()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        Run(templates.SmallProject, (pid, environment) =>
        {
            // The automation names, not the text: the time changes, and a PC with no battery collapses the battery.
            // A component renders a text block, so the name is what inspect shows.
            ChildProcess? tree = null;
            Wait.Until(() =>
            {
                tree = WinshellCli.Run(["inspect", "--pid", pid, "tree"], environment);
                return tree.Code == 0 && tree.Output.Contains("\"Clock\"", StringComparison.Ordinal);
            }, "the starter's bar to show its clock");
            Assert.Contains("\"Battery\"", tree!.Output, StringComparison.Ordinal);
        });
    }

    [Fact(Timeout = PatienceMs)]
    public void The_template_takes_WindowsAppSDK_from_the_Winshell_package()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        // A version of its own here would drift from Directory.Packages.props and fail restore with NU1605.
        Assert.DoesNotContain("Microsoft.WindowsAppSDK", File.ReadAllText(templates.SmallProject),
            StringComparison.Ordinal);
    }

    [Fact(Timeout = PatienceMs)]
    public void Packing_and_creating_the_template_leaves_the_user_PATH_as_it_was()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        Assert.Equal(templates.UserPathBefore, PackedTemplates.UserPath());
    }

    [Fact(Timeout = PatienceMs)]
    public void Widget_items_appear_for_each_name_including_case_and_escapes()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using var folder = new TempFolder();
        var projectFile = CopyTemplate(folder.Path);
        var document = XDocument.Load(projectFile);
        document.Root!.Add(new XElement("ItemGroup",
            new XElement("WinshellWidget", new XAttribute("Include", "first"),
                new XAttribute("Type", "global::PackedShell.FirstWidget")),
            new XElement("WinshellWidget", new XAttribute("Include", "First"),
                new XAttribute("Type", "PackedShell.CapitalWidget")),
            new XElement("WinshellWidget", new XAttribute("Include", "quote\"slash\\semi%3Bline%0Aend"),
                new XAttribute("Type", "PackedShell.SecondWidget"))));
        document.Save(projectFile);
        File.WriteAllText(Path.Combine(folder.Path, "AppShell.cs"), """
            using Microsoft.UI.Reactor.Core;
            using Microsoft.UI.Reactor.Hosting;
            using Microsoft.UI.Xaml.Controls;
            using Winshell;
            using static Microsoft.UI.Reactor.Factories;
            namespace PackedShell;
            public sealed class Placements : Component
            {
                public override Element Render() =>
                    Component<WidgetArea, WidgetAreaProps>(new WidgetAreaProps(Defaults:
                    [
                        new("first", new Dictionary<string, object> { ["value"] = "one" }),
                        new("First", new Dictionary<string, object> { ["value"] = "three" }),
                        new("quote\"slash\\semi;line\nend", new Dictionary<string, object> { ["value"] = "two" })
                    ]));
            }
            public sealed class AppShell : ShellConfiguration
            {
                public override void Start(Shell shell)
                {
                    var host = new ReactorHostControl { ComponentFactory = () => new Placements() };
                    var window = new PanelWindow { Width = 250, Height = 80, Content = host, Visible = true };
                    window.Closed += host.Dispose;
                }
            }
            public sealed class FirstWidget : Microsoft.UI.Reactor.Core.Component<WidgetContext>
            {
                public override Microsoft.UI.Reactor.Core.Element Render() =>
                    Microsoft.UI.Reactor.Factories.TextBlock("first:" + (string)Props.Settings["value"]);
            }
            public sealed class CapitalWidget : Microsoft.UI.Reactor.Core.Component<WidgetContext>
            {
                public override Microsoft.UI.Reactor.Core.Element Render() =>
                    Microsoft.UI.Reactor.Factories.TextBlock("First:" + (string)Props.Settings["value"]);
            }
            public sealed class SecondWidget : Microsoft.UI.Reactor.Core.Component<WidgetContext>
            {
                public override Microsoft.UI.Reactor.Core.Element Render() =>
                    Microsoft.UI.Reactor.Factories.TextBlock("second:" + (string)Props.Settings["value"]);
            }
            """);
        var project = ConfigurationProject.Find(projectFile, null, folder.Path, out var error);
        Assert.Null(error);
        var why = project!.Prepare(false, ConfigurationProject.Run);
        Assert.True(why is null, why);
        var generated = Assert.Single(Directory.GetFiles(Path.Combine(folder.Path, "obj"),
            "WinshellWidgetRegistration.g.cs", SearchOption.AllDirectories));
        var written = File.GetLastWriteTimeUtc(generated);
        var unchanged = ConfigurationProject.Run(
            "dotnet", ["build", projectFile, "-c", "Release", "-warnaserror", "--nologo"], folder.Path);
        Assert.True(unchanged.ExitCode == 0, unchanged.Output);
        Assert.Equal(generated, Assert.Single(Directory.GetFiles(Path.Combine(folder.Path, "obj"),
            "WinshellWidgetRegistration.g.cs", SearchOption.AllDirectories)));
        Assert.Equal(written, File.GetLastWriteTimeUtc(generated));

        Run(projectFile, (pid, environment) =>
        {
            ChildProcess? tree = null;
            Wait.Until(() =>
            {
                tree = WinshellCli.Run(["inspect", "--pid", pid, "tree"], environment);
                return tree.Code == 0 && tree.Output.Contains("second:two", StringComparison.Ordinal);
            }, "the generated widget factories to populate the window");
            Assert.Contains("first:one", tree!.Output, StringComparison.Ordinal);
            Assert.Contains("First:three", tree.Output, StringComparison.Ordinal);
            Assert.Contains("second:two", tree.Output, StringComparison.Ordinal);
        });
    }

    [Theory(Timeout = PatienceMs)]
    [InlineData("", false, "Type is required")]
    [InlineData("PackedShell.FirstWidget", true, "duplicated")]
    [InlineData("Bad.Type()", false, "must be a C# type name")]
    [InlineData("Missing.Widget", false, "error CS")]
    public void A_bad_widget_item_fails_the_build_and_names_the_item(string type, bool duplicate, string diagnostic)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using var folder = new TempFolder();
        var project = CopyTemplate(folder.Path);
        var document = XDocument.Load(project);
        var items = new XElement("ItemGroup",
            new XElement("WinshellWidget", new XAttribute("Include", "bad-widget"), new XAttribute("Type", type)));
        if (duplicate)
            items.Add(new XElement(items.Elements().Single()));
        document.Root!.Add(items);
        document.Save(project);
        var result = ConfigurationProject.Run("dotnet", ["build", project, "-warnaserror", "--nologo"], folder.Path);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(diagnostic, result.Output, StringComparison.Ordinal);
        if (diagnostic.StartsWith("error CS", StringComparison.Ordinal))
        {
            // The compiler cites the generated file. That line carries the item name and the type.
            var generated = Assert.Single(Directory.GetFiles(Path.Combine(folder.Path, "obj"),
                "WinshellWidgetRegistration.g.cs", SearchOption.AllDirectories));
            var source = File.ReadAllText(generated);
            Assert.Contains("WinshellWidgetRegistration.g.cs", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"bad-widget\"", source, StringComparison.Ordinal);
            Assert.Contains(type, source, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("bad-widget", result.Output, StringComparison.Ordinal);
        }
    }

    [Fact(Timeout = PatienceMs)]
    public void A_widget_item_builds_inside_a_git_repository()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using var folder = new TempFolder();
        var projectFile = CopyTemplate(folder.Path);
        var document = XDocument.Load(projectFile);
        document.Root!.Add(new XElement("ItemGroup",
            new XElement("WinshellWidget", new XAttribute("Include", "repo-clock"),
                new XAttribute("Type", "PackedShell.ClockWidget"))));
        document.Save(projectFile);
        File.WriteAllText(Path.Combine(folder.Path, "AppShell.cs"), """
            using Microsoft.UI.Xaml.Controls;
            using Winshell;
            namespace PackedShell;
            public sealed class AppShell : ShellConfiguration
            {
                public override void Start(Shell shell) { }
            }
            public sealed class ClockWidget : Microsoft.UI.Reactor.Core.Component<WidgetContext>
            {
                public override Microsoft.UI.Reactor.Core.Element Render() =>
                    Microsoft.UI.Reactor.Factories.TextBlock("clock");
            }
            """);
        // SourceLink embeds the registration file when a repository ignores it, which is how a .NET checkout treats
        // obj/. The other widget builds use a temp folder that is not a repository, so they never reach that failure.
        File.WriteAllText(Path.Combine(folder.Path, ".gitignore"), "bin/\nobj/\n");
        var git = ConfigurationProject.Run("git", ["init"], folder.Path);
        Assert.True(git.ExitCode == 0, git.Output);
        var built = ConfigurationProject.Run(
            "dotnet", ["build", projectFile, "-c", "Release", "-warnaserror", "--nologo"], folder.Path);
        Assert.True(built.ExitCode == 0, built.Output);
        var registration = File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(folder.Path, "obj"),
            "WinshellWidgetRegistration.g.cs", SearchOption.AllDirectories)));
        Assert.Contains("\"repo-clock\"", registration, StringComparison.Ordinal);
        Assert.Contains("PackedShell.ClockWidget", registration, StringComparison.Ordinal);
    }

    [Fact(Timeout = PatienceMs)]
    public void The_template_ignores_widgets_props()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var gitignore = File.ReadAllText(Path.Combine(Path.GetDirectoryName(templates.SmallProject)!, ".gitignore"));
        Assert.Contains("widgets.props", gitignore, StringComparison.Ordinal);
    }

    [Fact(Timeout = PatienceMs)]
    public void Installed_widgets_props_registers_a_library_widget()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using var root = new TempFolder();
        File.Copy(Path.Combine(Repo.Root, "global.json"), Path.Combine(root.Path, "global.json"));
        var shellDir = Path.Combine(root.Path, "shell");
        Directory.CreateDirectory(shellDir);
        var projectFile = CopyTemplate(shellDir);
        var libDir = Path.Combine(root.Path, "lib");
        Directory.CreateDirectory(libDir);
        File.Copy(Path.Combine(shellDir, "nuget.config"), Path.Combine(libDir, "nuget.config"));
        var version = XDocument.Load(projectFile).Descendants("PackageReference")
            .Single(element => element.Attribute("Include")!.Value == "Winshell")
            .Attribute("Version")!.Value;
        var libProject = Path.Combine(libDir, "WidgetLib.csproj");
        File.WriteAllText(libProject, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Winshell" Version="{{version}}" />
              </ItemGroup>
              <ItemGroup>
                <WinshellWidget Include="installed-clock" Type="WidgetLib.ClockWidget" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(libDir, "ClockWidget.cs"), """
            using Microsoft.UI.Xaml.Controls;
            using Winshell;

            namespace WidgetLib;

            public sealed class ClockWidget : Microsoft.UI.Reactor.Core.Component<WidgetContext>
            {
                public override Microsoft.UI.Reactor.Core.Element Render() =>
                    Microsoft.UI.Reactor.Factories.TextBlock("installed:" + (string)Props.Settings["value"]);
            }
            """);
        File.WriteAllText(Path.Combine(shellDir, "widgets.props"), """
            <Project>
              <ItemGroup>
                <ProjectReference Include="..\lib\WidgetLib.csproj" />
                <WinshellWidget Include="installed-clock" Type="WidgetLib.ClockWidget" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(shellDir, "AppShell.cs"), """
            using Microsoft.UI.Reactor.Core;
            using Microsoft.UI.Reactor.Hosting;
            using Microsoft.UI.Xaml.Controls;
            using Winshell;
            using static Microsoft.UI.Reactor.Factories;

            namespace PackedShell;

            public sealed class Placements : Component
            {
                public override Element Render() =>
                    Component<WidgetArea, WidgetAreaProps>(new WidgetAreaProps(Defaults:
                        [new("installed-clock", new Dictionary<string, object> { ["value"] = "one" })]));
            }

            public sealed class AppShell : ShellConfiguration
            {
                public override void Start(Shell shell)
                {
                    var host = new ReactorHostControl { ComponentFactory = () => new Placements() };
                    var window = new PanelWindow { Width = 250, Height = 80, Content = host, Visible = true };
                    window.Closed += host.Dispose;
                }
            }
            """);

        var library = ConfigurationProject.Run(
            "dotnet", ["build", libProject, "-warnaserror", "--nologo"], libDir);
        Assert.True(library.ExitCode == 0, library.Output);
        Assert.Empty(Directory.GetFiles(libDir, "WinshellWidgetRegistration.g.cs", SearchOption.AllDirectories));
        var listed = ConfigurationProject.Run(
            "dotnet", ["msbuild", libProject, "-getItem:WinshellWidget", "--nologo"], libDir);
        Assert.True(listed.ExitCode == 0, listed.Output);
        Assert.Contains("installed-clock", listed.Output, StringComparison.Ordinal);
        Assert.Contains("WidgetLib.ClockWidget", listed.Output, StringComparison.Ordinal);

        Run(projectFile, (pid, environment) =>
        {
            ChildProcess? tree = null;
            Wait.Until(() =>
            {
                tree = WinshellCli.Run(["inspect", "--pid", pid, "tree"], environment);
                return tree.Code == 0 && tree.Output.Contains("installed:one", StringComparison.Ordinal);
            }, "the widget from widgets.props to populate the window");
            Assert.Contains("installed:one", tree!.Output, StringComparison.Ordinal);
        });
        var registration = File.ReadAllText(Assert.Single(Directory.GetFiles(
            Path.Combine(shellDir, "obj"), "WinshellWidgetRegistration.g.cs", SearchOption.AllDirectories)));
        Assert.Contains("\"installed-clock\"", registration, StringComparison.Ordinal);
        Assert.Contains("WidgetLib.ClockWidget", registration, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(libDir, "WinshellWidgetRegistration.g.cs", SearchOption.AllDirectories));
    }

    [Fact(Timeout = PatienceMs)]
    public void A_command_line_property_selects_the_widgets_file()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        // widgets.props names a type the generator rejects. The build succeeds only when -p:WinshellWidgetsFile
        // imports staged.props instead. The path is absolute: a relative Exists resolves against Winshell.props,
        // not the project. The build runs from the parent directory.
        using var root = new TempFolder();
        File.Copy(Path.Combine(Repo.Root, "global.json"), Path.Combine(root.Path, "global.json"));
        var shellDir = Path.Combine(root.Path, "shell");
        Directory.CreateDirectory(shellDir);
        var projectFile = CopyTemplate(shellDir);
        File.WriteAllText(Path.Combine(shellDir, "widgets.props"), """
            <Project>
              <ItemGroup>
                <WinshellWidget Include="from-default" Type="Not A Type()" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(shellDir, "staged.props"), """
            <Project>
              <ItemGroup>
                <WinshellWidget Include="from-staged" Type="PackedShell.StagedWidget" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(shellDir, "StagedWidget.cs"), """
            using Microsoft.UI.Xaml.Controls;
            using Winshell;

            namespace PackedShell;

            public sealed class StagedWidget : Microsoft.UI.Reactor.Core.Component<WidgetContext>
            {
                public override Microsoft.UI.Reactor.Core.Element Render() =>
                    Microsoft.UI.Reactor.Factories.TextBlock((string)Props.Settings["value"]);
            }
            """);
        var staged = Path.Combine(shellDir, "staged.props");
        var built = ConfigurationProject.Run(
            "dotnet", ["build", projectFile, "-warnaserror", "--nologo", $"-p:WinshellWidgetsFile={staged}"], root.Path);
        Assert.True(built.ExitCode == 0, built.Output);
        var registration = File.ReadAllText(Assert.Single(Directory.GetFiles(
            Path.Combine(shellDir, "obj"), "WinshellWidgetRegistration.g.cs", SearchOption.AllDirectories)));
        Assert.Contains("\"from-staged\"", registration, StringComparison.Ordinal);
        Assert.Contains("PackedShell.StagedWidget", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("from-default", registration, StringComparison.Ordinal);
    }

    private string CopyTemplate(string folder)
    {
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(templates.SmallProject)!))
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        var project = Path.Combine(folder, Path.GetFileName(templates.SmallProject));
        var document = XDocument.Load(project);
        document.Root!.Add(new XElement("PropertyGroup", new XElement("TreatWarningsAsErrors", "true")));
        document.Save(project);
        return project;
    }

    private void Run(string projectFile, Action<string, (string Name, string Value)[]>? inspect = null)
    {
        var project = ConfigurationProject.Find(projectFile, null, Path.GetDirectoryName(projectFile)!, out var error);
        Assert.Null(error);
        var why = project!.Prepare(false, ConfigurationProject.Run);
        Assert.True(why is null, why);

        var dir = TempFolder.NewPath();
        (string Name, string Value)[] folders =
        [
            ("WINSHELL_CONFIG_DIR", Path.Combine(dir, "config")),
            ("WINSHELL_LOCAL_DIR", Path.Combine(dir, "local"))
        ];
        Directory.CreateDirectory(folders[0].Value);
        Directory.CreateDirectory(folders[1].Value);
        var output = new StringBuilder();
        var process = Start(project.Program, folders, output);
        try
        {
            ListAndKill(process, folders, output, inspect);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                process.WaitForExit();
            }

            process.Dispose();
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// Lists the shell, reads its log and kills it. The caller disposes <paramref name="process"/> after this returns.
    /// </summary>
    private void ListAndKill(Process process, (string Name, string Value)[] folders, StringBuilder output,
        Action<string, (string Name, string Value)[]>? inspect)
    {
        var pid = process.Id.ToString(CultureInfo.InvariantCulture);
        Wait.Until(() =>
            {
                if (process.HasExited)
                    Assert.Fail($"The shell exited with {process.ExitCode}: {output}");
                return Listed(folders).Any(row => row.GetProperty("pid").GetInt32() == process.Id);
            }, $"instance {pid} in winshell list");

        var log = WinshellCli.Run(["log", "--pid", pid], folders);
        log.AssertExitCode(0);
        Assert.Contains($"Winshell {templates.Version} on Windows", log.Output, StringComparison.Ordinal);

        inspect?.Invoke(pid, folders);

        var killed = WinshellCli.Run(["kill", "--pid", pid], folders);
        killed.AssertExitCode(0);
        Wait.Until(() => process.HasExited, "winshell kill to stop the shell");
        ChildProcess.Exited("The packed shell", process, output).AssertExitCode(0);
    }

    private static Process Start(string exe, (string Name, string Value)[] environment, StringBuilder output)
    {
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var (name, value) in environment)
            info.Environment[name] = value;
        var process = Process.Start(info)!;

        void Add(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
                lock (output)
                {
                    output.AppendLine(e.Data);
                }
        }

        process.OutputDataReceived += Add;
        process.ErrorDataReceived += Add;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static List<JsonElement> Listed((string Name, string Value)[] environment)
    {
        var result = WinshellCli.Run(["list", "--json"], environment);
        result.AssertExitCode(0);
        using var json = JsonDocument.Parse(result.Output);
        return json.RootElement.EnumerateArray().Select(row => row.Clone()).ToList();
    }
}

/// <summary>One pack and one copy of the template, shared by the tests that use it.</summary>
// xUnit constructs this fixture through IClassFixture; the test project never calls new.
public sealed class PackedTemplates : IDisposable
{
    private readonly string _work =
        TempFolder.NewPath();

    public PackedTemplates()
    {
        UserPathBefore = UserPath();
        var root = Repo.Root;
        // A version of its own each run. NuGet keeps a package by its version, so a repeated one would build what an
        // earlier run packed, and the test would pass on code it never packed. The release triple matches this
        // checkout: winshell inspect refuses a shell whose major.minor.patch differs from the CLI.
        var manifest = File.ReadAllText(Path.Combine(root, ".release-please-manifest.json"));
        using var manifestJson = JsonDocument.Parse(manifest);
        var release = manifestJson.RootElement.GetProperty(".").GetString()!;
        Version = release + "-test." + Guid.NewGuid().ToString("N")[..12];
        var packages = Path.Combine(_work, "packages");
        var packed = ConfigurationProject.Run("pwsh",
            ["-NoProfile", "-File", Path.Combine(root, "scripts", "pack.ps1"), "-Out", packages, "-Version", Version],
            root);
        Assert.True(packed.ExitCode == 0, packed.Output);

        var library = Path.Combine(packages, $"Winshell.{Version}.nupkg");
        AssertPackage(library, Version);
        var templates = Path.Combine(packages, $"Winshell.Templates.{Version}.nupkg");
        Assert.True(File.Exists(templates), $"No template package at {templates}");

        // A hive of the run's own keeps the templates out of the developer's. A DOTNET_CLI_HOME of its own would too, but
        // the SDK's first run in a new home appends that home's tools folder to the user PATH.
        var hive = Path.Combine(_work, "hive");
        var install = DotnetNew(hive, "install", templates);
        install.AssertExitCode(0);
        SmallProject = Instantiate(hive, packages, "winshell");
    }

    public string Version { get; }

    public string SmallProject { get; }

    /// <summary>The developer's own PATH, read before anything was packed or created.</summary>
    public (RegistryValueKind Kind, string? Value) UserPathBefore { get; }

    /// <summary>HKCU\Environment\Path as stored, so <c>%USERPROFILE%</c> stays unexpanded.</summary>
    public static (RegistryValueKind Kind, string? Value) UserPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey("Environment");
        if (key?.GetValue("Path", null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string value)
            return (RegistryValueKind.None, null);
        return (key.GetValueKind("Path"), value);
    }

    public void Dispose()
    {
        var cached = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } packages
            ? packages
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        foreach (var folder in new[] { _work, Path.Combine(cached, "winshell", Version) })
            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
    }

    private string Instantiate(string hive, string packages, string template)
    {
        var folder = Path.Combine(_work, template);
        var created = DotnetNew(hive, template, "-n", "PackedShell", "-o", folder);
        created.AssertExitCode(0);
        var project = Assert.Single(Directory.GetFiles(folder, "*.csproj"));
        File.WriteAllText(Path.Combine(folder, "nuget.config"), $"""
                                                                 <?xml version="1.0" encoding="utf-8"?>
                                                                 <configuration>
                                                                   <packageSources>
                                                                     <clear />
                                                                     <add key="local" value="{packages}" />
                                                                     <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                                                                   </packageSources>
                                                                 </configuration>
                                                                 """);
        return project;
    }

    private static ChildProcess DotnetNew(string hive, params string[] args)
    {
        var start = new ProcessStartInfo("dotnet");
        start.ArgumentList.Add("new");
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        start.ArgumentList.Add("--debug:custom-hive");
        start.ArgumentList.Add(hive);
        return ChildProcess.Run(start, TimeSpan.FromMinutes(5));
    }

    private static void AssertPackage(string nupkg, string version)
    {
        var packageFile = Path.Combine(Repo.Root, "src", "Winshell.Package", "Winshell.Package.csproj");
        var packageProject = XDocument.Load(packageFile);
        var packageDir = Path.GetDirectoryName(packageFile)!;
        var build = XDocument.Load(Path.Combine(Repo.Root, "Directory.Build.props"));
        var lib = "lib/" + build.Descendants("TargetFramework").Single().Value + "/";
        var referenced = packageProject.Descendants("ProjectReference").Select(reference =>
        {
            var include = reference.Attribute("Include")!.Value;
            var project = XDocument.Load(Path.GetFullPath(Path.Combine(packageDir, include)));
            var documents = project.Descendants("GenerateDocumentationFile").Any(element =>
                element.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
            return (Name: Path.GetFileNameWithoutExtension(include), Documents: documents);
        }).ToList();
        var libraries = referenced.Select(library => lib + library.Name + ".dll")
            .Concat(referenced.Where(library => library.Documents).Select(library => lib + library.Name + ".xml"))
            .Order(StringComparer.Ordinal)
            .ToList();

        using var zip = ZipFile.OpenRead(nupkg);
        var names = zip.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToList();
        Assert.Equal(libraries,
            names.Where(name => name.StartsWith("lib/", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        AssertProps(zip, packageDir);

        var extracted = TempFolder.NewPath();
        Directory.CreateDirectory(extracted);
        foreach (var library in libraries.Where(name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            var dll = Path.Combine(extracted, Path.GetFileName(library));
            zip.GetEntry(library)!.ExtractToFile(dll);
            Assert.Equal(version, InformationalVersion(dll));
            // ConfigurationProject stays internal. It lives in Services, so that assembly is the one to read.
            if (Path.GetFileName(library) == "Winshell.Services.dll")
                Assert.False(IsPublic(dll, "Winshell", "ConfigurationProject"));
        }

        Directory.Delete(extracted, true);

        var nuspec =
            zip.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        using var stream = nuspec.Open();
        var dependencies = XDocument.Load(stream).Descendants().Where(element => element.Name.LocalName == "dependency")
            .ToDictionary(element => element.Attribute("id")!.Value, element => element.Attribute("version")!.Value);
        // A new project gets WindowsAppSDK through this package, so these are the versions it restores.
        var pinned = XDocument.Load(Path.Combine(Repo.Root, "Directory.Packages.props")).Descendants("PackageVersion")
            .ToDictionary(element => element.Attribute("Include")!.Value,
                element => element.Attribute("Version")!.Value);
        Assert.Equal(pinned["Microsoft.UI.Reactor"], dependencies["Microsoft.UI.Reactor"]);
        Assert.Equal(pinned["Microsoft.WindowsAppSDK.Runtime"], dependencies["Microsoft.WindowsAppSDK.Runtime"]);
        var declared = packageProject.Descendants("PackageReference")
            .Where(reference => !IsPrivate(reference))
            .Select(reference => reference.Attribute("Include")!.Value)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Equal(declared, dependencies.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var id in declared)
            Assert.Equal(pinned[id], dependencies[id]);
    }

    /// <summary>A reference with <c>PrivateAssets</c> of <c>all</c> stays out of the packed package.</summary>
    private static bool IsPrivate(XElement reference)
    {
        var value = reference.Attribute("PrivateAssets")?.Value
                    ?? reference.Elements().FirstOrDefault(element => element.Name.LocalName == "PrivateAssets")?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("all", StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertProps(ZipArchive zip, string packageDir)
    {
        AssertPacked(zip, packageDir, "buildTransitive/Winshell.props");
    }

    private static void AssertPacked(ZipArchive zip, string packageDir, string path)
    {
        var entry = zip.GetEntry(path);
        Assert.NotNull(entry);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var packed = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        var source = File.ReadAllText(Path.Combine(packageDir, path.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(source, packed);
    }

    private static string InformationalVersion(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference)
                continue;
            var member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (member.Parent.Kind != HandleKind.TypeReference)
                continue;
            var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (reader.GetString(type.Name) != "AssemblyInformationalVersionAttribute")
                continue;
            var blob = reader.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1)
                continue;
            return blob.ReadSerializedString() ?? "";
        }

        throw new InvalidOperationException($"No informational version in {path}");
    }

    private static bool IsPublic(string path, string ns, string name)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            if (reader.GetString(type.Namespace) == ns && reader.GetString(type.Name) == name)
                return (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public;
        }

        throw new InvalidOperationException($"No type {ns}.{name} in {path}");
    }
}
