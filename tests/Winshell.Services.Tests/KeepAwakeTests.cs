using Winshell.Power;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class KeepAwakeTests
{
    private readonly List<string> _warnings = [];
    private readonly Log _log;

    public KeepAwakeTests()
    {
        _log = new Log { Sink = (_, _, message) => _warnings.Add(message) };
    }

    [TestMethod]
    public void Enabling_holds_the_PC_awake_and_Windows_accepts_the_request()
    {
        using var awake = new KeepAwake(_log);
        awake.Enabled = true;
        Assert.IsTrue(KeepAwake.AnyActive);
        awake.Enabled = false;
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void One_instance_turning_off_leaves_the_other_holding()
    {
        using var first = new KeepAwake(_log);
        using var second = new KeepAwake(_log);
        first.Enabled = true;
        second.Enabled = true;

        first.Enabled = false;

        Assert.IsTrue(second.Enabled);
        Assert.IsTrue(KeepAwake.AnyActive);
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void Disposing_an_enabled_instance_turns_it_off()
    {
        var awake = new KeepAwake(_log) { Enabled = true };
        awake.Dispose();
        Assert.IsFalse(awake.Enabled);
        Assert.IsEmpty(_warnings);
    }
}
