using System.Reflection;
using System.Text.RegularExpressions;

namespace Winshell.Core.Tests;

[TestClass]
public class ProductVersionTests
{
    [TestMethod]
    [DataRow("0.3.0", 0, 3, 0)]
    [DataRow("0.3.0+abcdef", 0, 3, 0)]
    [DataRow("0.3.0-dev", 0, 3, 0)]
    [DataRow("0.0.0-dev+sha", 0, 0, 0)]
    [DataRow("1.2", 1, 2, 0)]
    [DataRow("1", 1, 0, 0)]
    [DataRow("10.20.30-beta.1+build.7", 10, 20, 30)]
    [DataRow("", 0, 0, 0)]
    [DataRow("dev", 0, 0, 0)]
    [DataRow(null, 0, 0, 0)]
    public void Parse_reads_the_release_numbers(string? text, int major, int minor, int patch)
    {
        var version = ProductVersion.Parse(text);
        Assert.AreEqual(major, version.Major);
        Assert.AreEqual(minor, version.Minor);
        Assert.AreEqual(patch, version.Patch);
    }

    [TestMethod]
    public void The_assembly_version_is_the_release_please_manifest()
    {
        var manifest = Repo.ReleaseVersion();
        var informational = typeof(ProductVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        Assert.MatchesRegex($"^{Regex.Escape(manifest)}(\\+\\S+)?$", informational);
        Assert.AreEqual(informational, ProductVersion.Current.Text);
    }
}
