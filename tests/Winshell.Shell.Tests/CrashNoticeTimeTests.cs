namespace Winshell.ShellTests;

[TestClass]
public class CrashNoticeTimeTests
{
    [TestMethod]
    [DataRow(5u, 25)]
    [DataRow(25u, 25)]
    [DataRow(60u, 60)]
    [DataRow(300u, 300)]
    public void The_notice_follows_a_longer_setting_only(uint setting, int seconds)
    {
        Assert.AreEqual(TimeSpan.FromSeconds(seconds), ShellNotices.CrashTimeoutFor(setting));
    }
}
