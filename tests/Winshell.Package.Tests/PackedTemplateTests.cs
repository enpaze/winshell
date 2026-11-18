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
    public void The_starter_takes_WindowsAppSDK_from_the_Winshell_package()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        Assert.DoesNotContain("Microsoft.WindowsAppSDK", File.ReadAllText(templates.SmallProject),
            StringComparison.Ordinal);
    }

    [Fact(Timeout = PatienceMs)]
    public void Packing_and_creating_the_starter_leaves_the_user_PATH_as_it_was()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        Assert.Equal(templates.UserPathBefore, PackedTemplates.UserPath());
    }

    [Fact(Timeout = PatienceMs)]
    public void A_widget_item_is_ignored_and_generates_no_registration()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using var folder = new TempFolder();
        var projectFile = CopyStarter(folder.Path);
        var document = XDocument.Load(projectFile);
        document.Root!.Add(new XElement("ItemGroup",
            new XElement("WinshellWidget", new XAttribute("Include", "unused"),
                new XAttribute("Type", "Missing.Widget()"))));
        document.Save(projectFile);

        var built = ShellProject.Run(
            "dotnet", ["build", projectFile, "-c", "Release", "-warnaserror", "--nologo"], folder.Path);

        Assert.True(built.ExitCode == 0, built.Output);
        Assert.Empty(Directory.GetFiles(Path.Combine(folder.Path, "obj"),
            "WinshellWidgetRegistration.g.cs", SearchOption.AllDirectories));
    }

    [Theory(Timeout = PatienceMs)]
    [InlineData("MyShell")]
    [InlineData("")]
    public void A_shell_whose_only_file_is_a_top_level_Program_builds(string rootNamespace)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using var folder = new TempFolder();
        var projectFile = CopyStarter(folder.Path);
        var document = XDocument.Load(projectFile);
        document.Descendants("RootNamespace").Single().Value = rootNamespace;
        document.Save(projectFile);
        foreach (var source in Directory.GetFiles(folder.Path, "*.cs"))
            File.Delete(source);
        File.WriteAllText(Path.Combine(folder.Path, "Program.cs"), """
            Shell.Run(shell => shell.Log.Info("shell", "Started."));
            """);

        var built = ShellProject.Run(
            "dotnet", ["build", projectFile, "-c", "Release", "-warnaserror", "--nologo"], folder.Path);

        Assert.True(built.ExitCode == 0, built.Output);
    }

    [Fact(Timeout = PatienceMs)]
    public void A_library_widget_registered_in_code_shows_in_an_area()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using var root = new TempFolder();
        File.Copy(Path.Combine(Repo.Root, "global.json"), Path.Combine(root.Path, "global.json"));
        var shellDir = Path.Combine(root.Path, "shell");
        Directory.CreateDirectory(shellDir);
        var projectFile = CopyStarter(shellDir);
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
                <TargetFramework>net11.0-windows10.0.22621.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Winshell" Version="{{version}}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(libDir, "ClockWidget.cs"), """
            namespace WidgetLib;

            public sealed class ClockWidget : Widget
            {
                public override Element Render() => TextBlock("library:" + Setting("value", ""));
            }
            """);
        var shell = XDocument.Load(projectFile);
        shell.Root!.Add(new XElement("ItemGroup",
            new XElement("ProjectReference", new XAttribute("Include", @"..\lib\WidgetLib.csproj"))));
        shell.Save(projectFile);
        File.WriteAllText(Path.Combine(shellDir, "Program.cs"), """
            using Microsoft.UI.Reactor.Hosting;

            Shell.Run(shell =>
            {
                Widgets.Register<WidgetLib.ClockWidget>("library-clock");
                var host = new ReactorHostControl { ComponentFactory = () => new Placements() };
                var window = new PanelWindow { Width = 250, Height = 80, Content = host, Visible = true };
                window.Closed += host.Dispose;
            });

            public sealed class Placements : Component
            {
                public override Element Render() =>
                    Component<WidgetArea, WidgetAreaProps>(new WidgetAreaProps(Defaults:
                        [new("library-clock", new Dictionary<string, object> { ["value"] = "one" })]));
            }
            """);

        var library = ShellProject.Run(
            "dotnet", ["build", libProject, "-warnaserror", "--nologo"], libDir);
        Assert.True(library.ExitCode == 0, library.Output);

        Run(projectFile, (pid, environment) =>
        {
            ChildProcess? tree = null;
            Wait.Until(() =>
            {
                tree = WinshellCli.Run(["inspect", "--pid", pid, "tree"], environment);
                return tree.Code == 0 && tree.Output.Contains("library:one", StringComparison.Ordinal);
            }, "the library's widget to populate the window");
            Assert.Contains("library:one", tree!.Output, StringComparison.Ordinal);
        });
    }

    [Fact(Timeout = PatienceMs)]
    public void The_guides_widget_library_builds_against_the_package_and_exposes_its_component()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using var root = new TempFolder();
        File.Copy(Path.Combine(Repo.Root, "global.json"), Path.Combine(root.Path, "global.json"));
        var libDir = Path.Combine(root.Path, "MessageWidget");
        Directory.CreateDirectory(libDir);
        foreach (var file in Directory.GetFiles(Path.Combine(Repo.Root, "tests", "Winshell.Package.Tests", "MessageWidget")))
            File.Copy(file, Path.Combine(libDir, Path.GetFileName(file)));
        File.Copy(Path.Combine(Path.GetDirectoryName(templates.SmallProject)!, "nuget.config"),
            Path.Combine(libDir, "nuget.config"));
        var project = Path.Combine(libDir, "MessageWidget.csproj");
        var document = XDocument.Load(project);
        document.Descendants("PackageReference").Single(reference =>
            reference.Attribute("Include")!.Value == "Winshell").SetAttributeValue("Version", templates.Version);
        document.Save(project);

        var built = ShellProject.Run("dotnet", ["build", project, "-warnaserror", "--nologo"], libDir);

        Assert.True(built.ExitCode == 0, built.Output);
        var library = Assert.Single(Directory.GetFiles(Path.Combine(libDir, "bin"), "MessageWidget.dll",
            SearchOption.AllDirectories));
        Assert.True(PackedTemplates.IsPublic(library, "MessageWidget", "Message"));
    }

    private string CopyStarter(string folder)
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
        var project = ShellProject.Find(projectFile, null, Path.GetDirectoryName(projectFile)!, out var error);
        Assert.Null(error);
        var why = project!.Prepare(false, ShellProject.Run);
        Assert.True(why is null, why);

        var dir = TempFolder.NewPath();
        var folders = IsolatedFolders.Under(dir);
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
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var (name, value) in environment)
            info.Environment[name] = value;
        return ChildProcess.Start(info, output);
    }

    private static List<JsonElement> Listed((string Name, string Value)[] environment)
    {
        var result = WinshellCli.Run(["list", "--json"], environment);
        result.AssertExitCode(0);
        using var json = JsonDocument.Parse(result.Output);
        return json.RootElement.EnumerateArray().Select(row => row.Clone()).ToList();
    }
}

public sealed class PackedTemplates : IDisposable
{
    private readonly string _work =
        TempFolder.NewPath();

    public PackedTemplates()
    {
        UserPathBefore = UserPath();
        var root = Repo.Root;
        Version = Repo.ReleaseVersion() + "-test." + Guid.NewGuid().ToString("N")[..12];
        var packages = Path.Combine(_work, "packages");
        var packed = ShellProject.Run("pwsh",
            ["-NoProfile", "-File", Path.Combine(root, "scripts", "pack.ps1"), "-Out", packages, "-Version", Version],
            root);
        Assert.True(packed.ExitCode == 0, packed.Output);

        var library = Path.Combine(packages, $"Winshell.{Version}.nupkg");
        AssertPackage(library, Version);
        Assert.Equal([library], Directory.GetFiles(packages, "*.nupkg"));
        SmallProject = Instantiate(packages);
    }

    public string Version { get; }

    public string SmallProject { get; }

    public (RegistryValueKind Kind, string? Value) UserPathBefore { get; }

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

    private string Instantiate(string packages)
    {
        var folder = Path.Combine(_work, "starter");
        var created = WinshellCli.Run(["new", "starter", "-p", folder]);
        created.AssertExitCode(0);
        var project = Assert.Single(Directory.GetFiles(folder, "*.csproj"));
        var document = XDocument.Load(project);
        document.Descendants("PackageReference").Single(reference =>
            reference.Attribute("Include")!.Value == "Winshell").SetAttributeValue("Version", Version);
        document.Save(project);
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

    private static void AssertPackage(string nupkg, string version)
    {
        var packageFile = Path.Combine(Repo.Root, "src", "Winshell.Package", "Winshell.Package.csproj");
        var packageProject = XDocument.Load(packageFile);
        var packageDir = Path.GetDirectoryName(packageFile)!;
        var build = XDocument.Load(Path.Combine(Repo.Root, "Directory.Build.props"));
        var lib = "lib/" + build.Descendants("TargetFramework").Single().Value + "/";
        var libraries = packageProject.Descendants("ProjectReference")
            .Select(reference => lib + Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value) + ".dll")
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
            if (Path.GetFileName(library) == "Winshell.Services.dll")
                Assert.False(IsPublic(dll, "Winshell", "ShellProject"));
        }

        Directory.Delete(extracted, true);

        var nuspec =
            zip.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        using var stream = nuspec.Open();
        var dependencies = XDocument.Load(stream).Descendants().Where(element => element.Name.LocalName == "dependency")
            .ToDictionary(element => element.Attribute("id")!.Value, element => element.Attribute("version")!.Value);
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

    internal static bool IsPublic(string path, string ns, string name)
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
