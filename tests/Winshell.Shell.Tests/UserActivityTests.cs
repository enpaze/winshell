namespace Winshell.ShellTests;

/// <summary>
/// When <c>winshell inspect</c>'s real input holds back for the user. The input the shell sent itself, such as the move
/// of a <c>point</c> just before a <c>press</c>, must not count as theirs, and the tick count wraps after 49.7 days.
/// </summary>
public sealed class UserActivityTests
{
    [Theory]
    [InlineData(10_000u, 9_000u, null, true)] // the user, a second ago
    [InlineData(10_000u, 6_000u, null, false)] // the user, four seconds ago
    [InlineData(10_000u, 9_000u, 8_900u, false)] // stamped just after the shell sent its own
    [InlineData(10_000u, 9_000u, 9_100u, false)] // stamped before the shell's last send returned
    [InlineData(10_000u, 9_500u, 8_900u, true)] // the user, after the shell's own
    [InlineData(1_000u, uint.MaxValue - 999u, null, true)] // two seconds ago, across the wrap
    [InlineData(3_000u, uint.MaxValue - 999u, null, false)] // four seconds ago, across the wrap
    public void Input_from_someone_in_the_last_few_seconds_holds_it_back(uint now, uint lastInput, uint? sent,
        bool active)
    {
        Assert.Equal(active, RealInput.UserActive(now, lastInput, sent));
    }
}
