namespace Winshell.Tests;

[TestClass]
public class CliUsageTests
{
    [TestMethod]
    public void A_whole_number_pid_that_is_not_an_instance_exits_1()
    {
        var result = WinshellCli.RunWithEmptyConfig(["stop", "--pid", "0"]);

        result.AssertExitCode(1);
        Assert.AreEqual("", result.Output);
        Assert.AreEqual("No instance with process id 0." + Environment.NewLine, result.Error);
    }

    [TestMethod]
    public void Tail_of_int_max_prints_the_file()
    {
        using var dir = new TempFolder();
        var path = Path.Combine(dir.Path, "usage.log");
        File.WriteAllText(path, "one\r\ntwo\r\n");

        var result = WinshellCli.RunWithEmptyConfig(["log", path, "--tail", "2147483647"]);

        result.AssertExitCode(0);
        result.AssertNoError();
        Assert.AreEqual("one" + Environment.NewLine + "two" + Environment.NewLine, result.Output);
    }
}
