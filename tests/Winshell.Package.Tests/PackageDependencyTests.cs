using System.Xml.Linq;

namespace Winshell.Package.Tests;

/// <summary>
/// The package redeclares each library's packages. <c>PrivateAssets="all"</c> on the project references drops the
/// transitive ones, so this mirror is what a configuration restores.
/// </summary>
public class PackageDependencyTests
{
    [Fact]
    public void Reactor_is_pinned_to_one_exact_version()
    {
        var version = Pinned("Microsoft.UI.Reactor");

        Assert.DoesNotContain('[', version);
        Assert.DoesNotContain('(', version);
        Assert.DoesNotContain('*', version);
        Assert.DoesNotContain(',', version);
    }

    [Fact]
    public void The_Windows_App_SDK_packages_share_one_pin()
    {
        var versions = new[]
        {
            Pinned("Microsoft.WindowsAppSDK.WinUI"),
            Pinned("Microsoft.WindowsAppSDK.Foundation"),
            Pinned("Microsoft.WindowsAppSDK.Runtime")
        };

        Assert.Single(versions.Distinct());
    }

    [Fact]
    public void The_package_declares_the_libraries_public_package_dependencies()
    {
        var packageFile = Path.Combine(Repo.Root, "src", "Winshell.Package", "Winshell.Package.csproj");
        var package = XDocument.Load(packageFile);
        var packageDir = Path.GetDirectoryName(packageFile)!;
        var declared = PublicPackages(package);
        var needed = package.Descendants("ProjectReference")
            .Select(reference => Path.GetFullPath(Path.Combine(packageDir, reference.Attribute("Include")!.Value)))
            .SelectMany(path => PublicPackages(XDocument.Load(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(needed.Order(StringComparer.OrdinalIgnoreCase), declared.Order(StringComparer.OrdinalIgnoreCase));
    }

    private static string Pinned(string id)
    {
        var props = XDocument.Load(Path.Combine(Repo.Root, "Directory.Packages.props"));
        return props.Descendants("PackageVersion")
            .Single(element => element.Attribute("Include")!.Value == id)
            .Attribute("Version")!.Value;
    }

    /// <summary>Package ids that flow to a consumer. <c>PrivateAssets</c> of <c>all</c> does not.</summary>
    private static IEnumerable<string> PublicPackages(XDocument project)
    {
        return project.Descendants("PackageReference").Where(reference => !IsPrivate(reference))
            .Select(reference => reference.Attribute("Include")!.Value);
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
}
