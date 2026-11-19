using Winshell.Native;

namespace Winshell.ShellTests;

[TestClass]
public sealed class UserActivityTests
{
    [TestMethod]
    [DataRow(10_000u, 9_000u, new uint[0], true)]
    [DataRow(10_000u, 6_000u, new uint[0], false)]
    [DataRow(10_000u, 9_000u, new uint[] { 8_990, 9_000, 1_000 }, false)]
    [DataRow(10_000u, 9_040u, new uint[] { 8_990, 9_000, 1_000 }, false)]
    [DataRow(10_000u, 9_000u, new uint[] { 8_990, 9_000, 8_980 }, true)]
    [DataRow(10_000u, 9_100u, new uint[] { 8_990, 9_000, 1_000 }, true)]
    [DataRow(10_000u, 9_500u, new uint[] { 8_990, 9_000, 1_000, 9_490, 9_500, 8_995 }, false)]
    [DataRow(10_000u, 9_500u, new uint[] { 9_500, 9_500, 1_000, 9_500, 9_500, 9_500 }, false)]
    [DataRow(1_000u, uint.MaxValue - 999u, new uint[0], true)]
    [DataRow(3_000u, uint.MaxValue - 999u, new uint[0], false)]
    [DataRow(1_000u, 2u, new[] { uint.MaxValue - 5u, 3u, 500_000u }, false)]
    public void Input_from_someone_in_the_last_few_seconds_holds_it_back(uint now, uint lastInput, uint[] sends,
        bool active)
    {
        var sent = sends.Chunk(3).Select(send => new InjectedInput(send[0], send[1], send[2])).ToList();

        Assert.AreEqual(active, SentInput.WithinQuiet(now, lastInput, sent));
    }
}
