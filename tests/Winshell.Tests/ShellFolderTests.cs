namespace Winshell.Tests;

public class ShellFolderTests
{
    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(@"..\..\somewhere")]
    [InlineData("work/old")]
    [InlineData(@"C:\config")]
    public void A_name_that_is_not_one_folder_is_a_usage_error(string name)
    {
        var error = Assert.Throws<ArgumentException>(() => CliParser.Parse(["run", "-s", name]));

        Assert.Contains("Use -p for a path", error.Message);
    }

    [Theory]
    [InlineData("work")]
    [InlineData("my shell")]
    [InlineData("v1.2")]
    public void A_folder_name_is_kept(string name)
    {
        Assert.Equal(name, CliParser.Parse(["run", "-s", name]).Text(CliFlags.Shell));
    }
}
