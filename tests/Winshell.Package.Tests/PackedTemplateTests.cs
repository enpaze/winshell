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

[TestClass]
[TestCategory("Package")]
[DoNotParallelize]
public sealed class PackedTemplateTests
{
    private static PackedTemplates _templates = null!;

    [ClassInitialize]
    public static void Pack(TestContext context)
    {
        _templates = new PackedTemplates();
    }

    [ClassCleanup]
    public static void Remove()
    {
        _templates.Dispose();
    }

    private const int PatienceMs = 1_800_000;

    [TestMethod, Timeout(PatienceMs)]
    public void The_starter_shows_its_clock_and_battery_and_is_listed_and_stopped()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        Run(_templates.SmallProject, (pid, environment) =>
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

    [TestMethod, Timeout(PatienceMs)]
    public void The_starter_takes_WindowsAppSDK_from_the_Winshell_package()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        Assert.DoesNotContain("Microsoft.WindowsAppSDK", File.ReadAllText(_templates.SmallProject),
            StringComparison.Ordinal);
    }

    [TestMethod, Timeout(PatienceMs)]
    public void Packing_and_creating_the_starter_leaves_the_user_PATH_as_it_was()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        Assert.AreEqual(_templates.UserPathBefore, PackedTemplates.UserPath());
    }

    [TestMethod, Timeout(PatienceMs)]
    public void A_widget_item_is_ignored_and_generates_no_registration()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        using var folder = new TempFolder();
        var projectFile = CopyStarter(folder.Path);
        var document = XDocument.Load(projectFile);
        document.Root!.Add(new XElement("ItemGroup",
            new XElement("WinshellWidget", new XAttribute("Include", "unused"),
                new XAttribute("Type", "Missing.Widget()"))));
        document.Save(projectFile);

        var built = ShellProject.Run(
            "dotnet", ["build", projectFile, "-c", "Release", "-warnaserror", "--nologo"], folder.Path);

        Assert.IsTrue(built.ExitCode == 0, built.Output);
        Assert.IsEmpty(Directory.GetFiles(Path.Combine(folder.Path, "obj"),
            "WinshellWidgetRegistration.g.cs", SearchOption.AllDirectories));
    }

    [TestMethod, Timeout(PatienceMs)]
    [DataRow("MyShell")]
    [DataRow("")]
    public void A_shell_whose_only_file_is_a_top_level_Program_builds(string rootNamespace)
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
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

        Assert.IsTrue(built.ExitCode == 0, built.Output);
    }

    [TestMethod, Timeout(PatienceMs)]
    public void A_shell_declares_a_preference_as_a_partial_property_the_package_implements()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        using var folder = new TempFolder();
        var projectFile = CopyStarter(folder.Path);
        File.WriteAllText(Path.Combine(folder.Path, "ShellPreferences.cs"), """
            namespace MyShell;

            public sealed partial class ShellPreferences : Preferences
            {
                [Preference]
                [TomlComment("Show 13:00 rather than 1:00 PM.")]
                public partial bool Clock24 { get; set; } = true;
            }
            """);

        var built = ShellProject.Run(
            "dotnet", ["build", projectFile, "-c", "Release", "-warnaserror", "--nologo"], folder.Path);

        Assert.IsTrue(built.ExitCode == 0, built.Output);
    }

    [TestMethod, Timeout(PatienceMs)]
    public void A_shell_names_its_appearance_with_only_the_package_usings()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        using var folder = new TempFolder();
        var projectFile = CopyStarter(folder.Path);
        foreach (var source in Directory.GetFiles(folder.Path, "*.cs"))
            File.Delete(source);
        File.WriteAllText(Path.Combine(folder.Path, "Program.cs"), """
            Shell.Run(shell => shell.Appearance = Appearance.Dark);
            """);

        var built = ShellProject.Run(
            "dotnet", ["build", projectFile, "-c", "Release", "-warnaserror", "--nologo"], folder.Path);

        Assert.IsTrue(built.ExitCode == 0, built.Output);
    }

    [TestMethod, Timeout(PatienceMs)]
    [DataRow(false)]
    [DataRow(true)]
    public void A_library_widget_the_program_never_names_is_found_and_shows_in_an_area(bool packed)
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
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
        var libName = "WidgetLib" + Guid.NewGuid().ToString("N");
        var libProject = Path.Combine(libDir, libName + ".csproj");
        File.WriteAllText(libProject, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFramework>net11.0-windows10.0.22621.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <Version>{{version}}</Version>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Winshell" Version="{{version}}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(libDir, "LibraryClockWidget.cs"), """
            namespace WidgetLib;

            public sealed class LibraryClockWidget : Widget
            {
                public override Element Render() => TextBlock("library:" + Setting("value", ""));
            }
            """);
        var shell = XDocument.Load(projectFile);
        if (packed)
        {
            var feed = Path.Combine(root.Path, "feed");
            var pack = ShellProject.Run("dotnet", ["pack", libProject, "-o", feed, "-warnaserror", "--nologo"], libDir);
            Assert.IsTrue(pack.ExitCode == 0, pack.Output);
            var config = Path.Combine(shellDir, "nuget.config");
            var sources = XDocument.Load(config);
            sources.Descendants("packageSources").Single()
                .Add(new XElement("add", new XAttribute("key", "widgets"), new XAttribute("value", feed)));
            sources.Save(config);
            shell.Root!.Add(new XElement("ItemGroup",
                new XElement("PackageReference", new XAttribute("Include", libName), new XAttribute("Version", version))));
        }
        else
        {
            var library = ShellProject.Run("dotnet", ["build", libProject, "-warnaserror", "--nologo"], libDir);
            Assert.IsTrue(library.ExitCode == 0, library.Output);
            shell.Root!.Add(new XElement("ItemGroup",
                new XElement("ProjectReference", new XAttribute("Include", $@"..\lib\{libName}.csproj"))));
        }

        shell.Save(projectFile);
        File.WriteAllText(Path.Combine(shellDir, "Program.cs"), """
            Shell.Run(shell =>
            {
                shell.Add(new PanelWindow<Placements> { Width = 250, Height = 80 });
            });

            public sealed class Placements : Component
            {
                public override Element Render() =>
                    WidgetArea(defaults: [new("library-clock", new Dictionary<string, object> { ["value"] = "one" })]);
            }
            """);

        try
        {
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
        finally
        {
            PackedTemplates.Forget(libName, version);
        }
    }

    [TestMethod, Timeout(PatienceMs)]
    public void The_guides_widget_library_builds_against_the_package_and_exposes_its_component()
    {
        CurrentTest.Cancellation.ThrowIfCancellationRequested();
        using var root = new TempFolder();
        File.Copy(Path.Combine(Repo.Root, "global.json"), Path.Combine(root.Path, "global.json"));
        var libDir = Path.Combine(root.Path, "MessageWidget");
        Directory.CreateDirectory(libDir);
        foreach (var file in Directory.GetFiles(Path.Combine(Repo.Root, "tests", "Winshell.Package.Tests", "MessageWidget")))
            File.Copy(file, Path.Combine(libDir, Path.GetFileName(file)));
        File.Copy(Path.Combine(Path.GetDirectoryName(_templates.SmallProject)!, "nuget.config"),
            Path.Combine(libDir, "nuget.config"));
        var project = Path.Combine(libDir, "MessageWidget.csproj");
        var document = XDocument.Load(project);
        document.Descendants("PackageReference").Single(reference =>
            reference.Attribute("Include")!.Value == "Winshell").SetAttributeValue("Version", _templates.Version);
        document.Save(project);

        var built = ShellProject.Run("dotnet", ["build", project, "-warnaserror", "--nologo"], libDir);

        Assert.IsTrue(built.ExitCode == 0, built.Output);
        var library = Assert.ContainsSingle(Directory.GetFiles(Path.Combine(libDir, "bin"), "MessageWidget.dll",
            SearchOption.AllDirectories));
        Assert.IsTrue(PackedTemplates.IsPublic(library, "MessageWidget", "Message"));
    }

    private string CopyStarter(string folder)
    {
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(_templates.SmallProject)!))
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        var project = Path.Combine(folder, Path.GetFileName(_templates.SmallProject));
        var document = XDocument.Load(project);
        document.Root!.Add(new XElement("PropertyGroup", new XElement("TreatWarningsAsErrors", "true")));
        document.Save(project);
        return project;
    }

    private void Run(string projectFile, Action<string, (string Name, string Value)[]>? inspect = null)
    {
        var project = ShellProject.Find(projectFile, null, Path.GetDirectoryName(projectFile)!, out var error);
        Assert.IsNull(error);
        var why = project!.Prepare(false, ShellProject.Run);
        Assert.IsTrue(why is null, why);

        var dir = TempFolder.NewPath();
        var folders = IsolatedFolders.Under(dir);
        Directory.CreateDirectory(folders[0].Value);
        Directory.CreateDirectory(folders[1].Value);
        var output = new StringBuilder();
        var process = Start(project.Program, folders, output);
        try
        {
            ListAndStop(process, folders, output, inspect);
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

    private void ListAndStop(Process process, (string Name, string Value)[] folders, StringBuilder output,
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
        Assert.Contains($"Winshell {_templates.Version} on Windows", log.Output, StringComparison.Ordinal);

        inspect?.Invoke(pid, folders);

        var stopped = WinshellCli.Run(["stop", "--pid", pid], folders);
        stopped.AssertExitCode(0);
        Wait.Until(() => process.HasExited, "winshell stop to stop the shell");
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

internal sealed class PackedTemplates : IDisposable
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
        Assert.IsTrue(packed.ExitCode == 0, packed.Output);

        var library = Path.Combine(packages, $"Winshell.{Version}.nupkg");
        AssertPackage(library, Version);
        Assert.AreSequenceEqual([library], Directory.GetFiles(packages, "*.nupkg"));
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
        foreach (var folder in new[] { _work, Cached("Winshell", Version) })
            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
    }

    internal static void Forget(string id, string version)
    {
        try
        {
            Directory.Delete(Cached(id, version), true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string Cached(string id, string version)
    {
        var cached = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } packages
            ? packages
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        return Path.Combine(cached, id.ToLowerInvariant(), version);
    }

    private string Instantiate(string packages)
    {
        var folder = Path.Combine(_work, "starter");
        var created = WinshellCli.Run(["new", "starter", "-p", folder]);
        created.AssertExitCode(0);
        var project = Assert.ContainsSingle(Directory.GetFiles(folder, "*.csproj"));
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
        string[] runtime = ["Winshell.Core", "Winshell.Native", "Winshell.Services", "Winshell.Shell"];
        const string generator = "Winshell.Generators";
        Assert.AreSequenceEqual(runtime.Append(generator).Order(StringComparer.Ordinal),
            packageProject.Descendants("ProjectReference")
                .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
                .Order(StringComparer.Ordinal));
        var libraries = runtime.Select(name => lib + name + ".dll").ToList();

        using var zip = ZipFile.OpenRead(nupkg);
        var names = zip.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToList();
        Assert.AreSequenceEqual(libraries,
            names.Where(name => name.StartsWith("lib/", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.AreSequenceEqual(["analyzers/dotnet/cs/" + generator + ".dll"],
            names.Where(name => name.StartsWith("analyzers/", StringComparison.Ordinal)));
        AssertProps(zip, packageDir);

        var extracted = TempFolder.NewPath();
        Directory.CreateDirectory(extracted);
        foreach (var library in libraries.Where(name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            var dll = Path.Combine(extracted, Path.GetFileName(library));
            zip.GetEntry(library)!.ExtractToFile(dll);
            Assert.AreEqual(version, InformationalVersion(dll));
            if (Path.GetFileName(library) == "Winshell.Services.dll")
                Assert.IsFalse(IsPublic(dll, "Winshell", "ShellProject"));
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
        Assert.AreEqual(pinned["Microsoft.UI.Reactor"], dependencies["Microsoft.UI.Reactor"]);
        Assert.AreEqual(pinned["Microsoft.WindowsAppSDK.Runtime"], dependencies["Microsoft.WindowsAppSDK.Runtime"]);
        var declared = packageProject.Descendants("PackageReference")
            .Where(reference => !IsPrivate(reference))
            .Select(reference => reference.Attribute("Include")!.Value)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.AreSequenceEqual(declared, dependencies.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var id in declared)
            Assert.AreEqual(pinned[id], dependencies[id]);
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
        Assert.IsNotNull(entry);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var packed = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        var source = File.ReadAllText(Path.Combine(packageDir, path.Replace('/', Path.DirectorySeparatorChar)))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.AreEqual(source, packed);
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
