namespace Winshell.Tests;

[TestClass]
public class SeveralInstancesTests
{
    [TestMethod]
    public void One_instance_is_used_when_no_selector_was_given()
    {
        var only = Instance("only", @"C:\only\MyShell.exe");
        var choice = InstanceSelection.ChooseUnselected([only]);

        Assert.AreEqual(0, choice.Code);
        Assert.AreSame(only, choice.Info);
    }

    [TestMethod]
    public void No_instance_is_a_miss()
    {
        var choice = InstanceSelection.ChooseUnselected([]);

        Assert.AreEqual(1, choice.Code);
        Assert.AreEqual("No running instances. Start one with winshell run.", choice.Error);
        Assert.IsNull(choice.Info);
    }

    [TestMethod]
    public void Several_instances_are_a_command_line_error_that_names_each()
    {
        var choice = InstanceSelection.ChooseUnselected([
            Instance("aaa", @"C:\a\MyShell.exe"),
            Instance("bbb", @"C:\b\MyShell.exe")
        ]);

        Assert.AreEqual(2, choice.Code);
        Assert.IsNull(choice.Info);
        Assert.AreEqual(
            @"Several instances are running; pick one with -i: aaa (C:\a\MyShell.exe), bbb (C:\b\MyShell.exe).",
            choice.Error);
    }

    private static InstanceInfo Instance(string id, string config)
    {
        return new InstanceInfo(id, 1, "shell", config, DateTime.UnixEpoch, "pipe-" + id, "log-" + id);
    }
}
