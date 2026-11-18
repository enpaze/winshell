namespace Winshell.Package.Tests;

public sealed class ShellPropertiesTests
{
    [Fact]
    public void The_shell_properties_reach_the_program_as_written()
    {
        var metadata = Metadata("""
            <ShellName>my-shell</ShellName>
            <ShellDataDir>data</ShellDataDir>
            <ShellStateDir>$BASE\state</ShellStateDir>
            <ShellCacheDir>D:\cache</ShellCacheDir>
            """);

        Assert.Equal(new Dictionary<string, string>
        {
            ["ShellName"] = "my-shell",
            ["ShellDataDir"] = "data",
            ["ShellStateDir"] = @"$BASE\state",
            ["ShellCacheDir"] = @"D:\cache"
        }, metadata);
    }

    [Fact]
    public void An_environment_variable_stays_for_the_shell_to_expand_and_an_escaped_percent_is_one_percent()
    {
        var metadata = Metadata("""
            <ShellDataDir>%LOCALAPPDATA%\mine</ShellDataDir>
            <ShellCacheDir>%25DEVROOT%\cache</ShellCacheDir>
            """);

        Assert.Equal(@"%LOCALAPPDATA%\mine", metadata["ShellDataDir"]);
        Assert.Equal(@"%DEVROOT%\cache", metadata["ShellCacheDir"]);
    }

    [Fact]
    public void A_project_that_sets_none_carries_none()
    {
        Assert.DoesNotContain(Metadata("").Keys, key => key.StartsWith("Shell", StringComparison.Ordinal));
    }

    private static Dictionary<string, string> Metadata(string properties)
    {
        return PackageProps.Items(properties, "AssemblyAttribute", "GetAssemblyAttributes")
            .Where(item => item.GetProperty("Identity").GetString() == "System.Reflection.AssemblyMetadata")
            .ToDictionary(item => item.GetProperty("_Parameter1").GetString()!,
                item => item.GetProperty("_Parameter2").GetString()!);
    }
}
