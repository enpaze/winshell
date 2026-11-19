namespace Winshell.Tests;

[TestClass]
public class CliErrorTests
{
    [TestMethod]
    public void An_empty_path_on_stop_is_one_sentence()
    {
        var result = WinshellCli.RunWithEmptyConfig("stop", "-p", "");

        result.AssertExitCode(1);
        Assert.AreEqual("", result.Output);
        Assert.StartsWith("winshell stop:", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", result.Error, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Verbose_includes_the_stack()
    {
        var result = WinshellCli.RunWithEmptyConfig("-v", "stop", "-p", "");

        result.AssertExitCode(1);
        Assert.Contains("winshell stop:", result.Error, StringComparison.Ordinal);
        Assert.Contains("   at ", result.Error, StringComparison.Ordinal);
    }

    [TestMethod]
    public void With_nothing_running_list_says_how_to_start_one_and_stop_does_not()
    {
        var local = ("WINSHELL_LOCAL_DIR", TempFolder.NewPath());
        var list = WinshellCli.Run(["list"], local);
        var stop = WinshellCli.Run(["stop"], local);

        Assert.AreEqual((0, "No running instances. Start one with winshell run." + Environment.NewLine, ""),
            (list.Code, list.Output, list.Error));
        Assert.AreEqual((1, "", "No running instances." + Environment.NewLine), (stop.Code, stop.Output, stop.Error));
    }
}
