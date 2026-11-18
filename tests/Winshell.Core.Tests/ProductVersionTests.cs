using System.Reflection;
using System.Text.RegularExpressions;

namespace Winshell.Core.Tests;

public class ProductVersionTests
{
    [Theory]
    [InlineData("0.3.0", 0, 3, 0)]
    [InlineData("0.3.0+abcdef", 0, 3, 0)]
    [InlineData("0.3.0-dev", 0, 3, 0)]
    [InlineData("0.0.0-dev+sha", 0, 0, 0)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("1", 1, 0, 0)]
    [InlineData("10.20.30-beta.1+build.7", 10, 20, 30)]
    [InlineData("", 0, 0, 0)]
    [InlineData("dev", 0, 0, 0)]
    [InlineData(null, 0, 0, 0)]
    public void Parse_reads_the_release_numbers(string? text, int major, int minor, int patch)
    {
        var version = ProductVersion.Parse(text);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
    }

    [Fact]
    public void The_assembly_version_is_the_release_please_manifest()
    {
        var manifest = Repo.ReleaseVersion();
        var informational = typeof(ProductVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        Assert.Matches($"^{Regex.Escape(manifest)}(\\+\\S+)?$", informational);
        Assert.Equal(informational, ProductVersion.Current.Text);
    }
}
