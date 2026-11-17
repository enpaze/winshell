using Winshell.Power;

namespace Winshell.Services.Tests;

public sealed class KeepAwakeTests
{
    private readonly List<string> _warnings = [];
    private readonly Log _log;

    public KeepAwakeTests()
    {
        _log = new Log { Sink = (_, _, message) => _warnings.Add(message) };
    }

    [Fact]
    public void Enabling_holds_the_PC_awake_and_Windows_accepts_the_request()
    {
        using var awake = new KeepAwake(_log);
        awake.Enabled = true;
        Assert.True(KeepAwake.AnyActive);
        awake.Enabled = false;
        Assert.Empty(_warnings);
    }

    [Fact]
    public void One_instance_turning_off_leaves_the_other_holding()
    {
        using var first = new KeepAwake(_log);
        using var second = new KeepAwake(_log);
        first.Enabled = true;
        second.Enabled = true;

        first.Enabled = false;

        Assert.True(second.Enabled);
        Assert.True(KeepAwake.AnyActive);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void Disposing_an_enabled_instance_turns_it_off()
    {
        var awake = new KeepAwake(_log) { Enabled = true };
        awake.Dispose();
        Assert.False(awake.Enabled);
        Assert.Empty(_warnings);
    }
}
