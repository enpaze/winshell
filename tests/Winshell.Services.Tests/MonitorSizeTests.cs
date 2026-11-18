namespace Winshell.Services.Tests;

[TestClass]
public class MonitorSizeTests
{
    [TestMethod]
    public void Inches_become_millimeters_and_a_missing_side_is_no_size()
    {
        Assert.AreEqual((254d, 127d), MonitorSize.FromInches(10, 5));
        Assert.AreEqual((0d, 0d), MonitorSize.FromInches(0, 10));
        Assert.AreEqual((0d, 0d), MonitorSize.FromInches(-1, 10));
        Assert.AreEqual((0d, 0d), MonitorSize.FromInches(null, 10));
        Assert.AreEqual((0d, 0d), MonitorSize.Read(""));
    }

    [TestMethod]
    public void A_monitor_is_asked_once_and_a_late_answer_fills_the_size_in()
    {
        var answer = new TaskCompletionSource<(double, double)>();
        var queries = 0;
        var cache = new MonitorSizeCache(_ =>
        {
            queries++;
            return answer.Task;
        });

        Assert.AreEqual((0d, 0d), cache.Read("monitor"));
        Assert.AreEqual((0d, 0d), cache.Read("monitor"));
        answer.SetResult((697, 393));
        Assert.AreEqual((697d, 393d), cache.Read("monitor"));
        Assert.AreEqual(1, queries);

        Assert.AreEqual((0d, 0d), cache.Read(""));
        Assert.AreEqual(1, queries);
    }
}
