namespace Winshell.Tests;

[TestClass]
public class ShellFolderTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow(@"..\..\somewhere")]
    [DataRow("work/old")]
    [DataRow(@"C:\config")]
    public void A_name_that_is_not_one_folder_is_a_usage_error(string name)
    {
        var error = Assert.ThrowsExactly<ArgumentException>(() => CliParser.Parse(["run", "-s", name]));

        Assert.Contains("Use -p for a path", error.Message);
    }

    [TestMethod]
    [DataRow("work")]
    [DataRow("my shell")]
    [DataRow("v1.2")]
    public void A_folder_name_is_kept(string name)
    {
        Assert.AreEqual(name, CliParser.Parse(["run", "-s", name]).Text(CliFlags.Shell));
    }
}
