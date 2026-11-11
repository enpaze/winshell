using Winshell.Native;

namespace Winshell.ShellTests;

/// <summary>
/// When <c>winshell inspect</c>'s real input holds back for the user. Input stamped during one of the shell's own
/// sends, such as the move of a <c>point</c> or a focus takeover's tap, is not theirs, but what that send hid may be.
/// The tick count wraps after 49.7 days.
/// </summary>
public sealed class UserActivityTests
{
    // Each send is three numbers: its start, its end, and the last input before it.
    [Theory]
    [InlineData(10_000u, 9_000u, new uint[0], true)] // the user, a second ago
    [InlineData(10_000u, 6_000u, new uint[0], false)] // the user, four seconds ago
    [InlineData(10_000u, 9_000u, new uint[] { 8_990, 9_000, 1_000 }, false)] // the shell's own send, over old input
    [InlineData(10_000u, 9_040u, new uint[] { 8_990, 9_000, 1_000 }, false)] // stamped a few ticks after it returned
    [InlineData(10_000u, 9_000u, new uint[] { 8_990, 9_000, 8_980 }, true)] // its send hid the user's hotkey press
    [InlineData(10_000u, 9_100u, new uint[] { 8_990, 9_000, 1_000 }, true)] // the user, after the shell's send
    [InlineData(10_000u, 9_500u, new uint[] { 8_990, 9_000, 1_000, 9_490, 9_500, 8_995 }, false)] // send over send
    [InlineData(10_000u, 9_500u, new uint[] { 9_500, 9_500, 1_000, 9_500, 9_500, 9_500 }, false)] // two in one tick
    [InlineData(1_000u, uint.MaxValue - 999u, new uint[0], true)] // two seconds ago, across the wrap
    [InlineData(3_000u, uint.MaxValue - 999u, new uint[0], false)] // four seconds ago, across the wrap
    [InlineData(1_000u, 2u, new[] { uint.MaxValue - 5u, 3u, 500_000u }, false)] // during a send that crossed the wrap
    public void Input_from_someone_in_the_last_few_seconds_holds_it_back(uint now, uint lastInput, uint[] sends,
        bool active)
    {
        var sent = sends.Chunk(3).Select(send => new Send(send[0], send[1], send[2])).ToList();

        Assert.Equal(active, SentInput.WithinQuiet(now, lastInput, sent));
    }
}
