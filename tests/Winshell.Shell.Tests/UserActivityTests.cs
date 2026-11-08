namespace Winshell.ShellTests;

/// <summary>
/// When <c>winshell inspect</c>'s real input holds back for the user. Input stamped during one of the shell's own
/// sends, such as the move of a <c>point</c> just before a <c>press</c>, is not theirs; anything before or after it is.
/// The tick count wraps after 49.7 days.
/// </summary>
public sealed class UserActivityTests
{
    [Theory]
    [InlineData(10_000u, 9_000u, new uint[0], true)] // the user, a second ago
    [InlineData(10_000u, 6_000u, new uint[0], false)] // the user, four seconds ago
    [InlineData(10_000u, 9_000u, new uint[] { 8_990, 9_000 }, false)] // stamped during the shell's own send
    [InlineData(10_000u, 9_040u, new uint[] { 8_990, 9_000 }, false)] // stamped a few ticks after it returned
    [InlineData(10_000u, 9_100u, new uint[] { 8_990, 9_000 }, true)] // the user, after the shell's send
    [InlineData(10_000u, 8_900u, new uint[] { 8_990, 9_000 }, true)] // the user, before the shell's send
    [InlineData(10_000u, 9_000u, new uint[] { 8_990, 9_000, 9_500, 9_510 }, false)] // an earlier send of the shell's
    [InlineData(1_000u, uint.MaxValue - 999u, new uint[0], true)] // two seconds ago, across the wrap
    [InlineData(3_000u, uint.MaxValue - 999u, new uint[0], false)] // four seconds ago, across the wrap
    [InlineData(1_000u, 2u, new[] { uint.MaxValue - 5u, 3u }, false)] // during a send that crossed the wrap
    public void Input_from_someone_in_the_last_few_seconds_holds_it_back(uint now, uint lastInput, uint[] sends,
        bool active)
    {
        var sent = sends.Chunk(2).Select(send => (send[0], send[1]));

        Assert.Equal(active, RealInput.UserActive(now, lastInput, sent));
    }
}
