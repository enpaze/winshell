using Winshell.Native;

namespace Winshell.ShellTests;

[TestClass]
public class SystemChangesTests
{
    [TestMethod]
    public void The_broadcasts_of_one_change_are_followed_once()
    {
        var changes = new SystemChanges();

        Assert.IsTrue(changes.Add(SystemChanges.From(PInvoke.WM_SETTINGCHANGE, 0)));
        Assert.IsFalse(changes.Add(SystemChanges.From(PInvoke.WM_SETTINGCHANGE, 0)));
        Assert.IsFalse(changes.Add(SystemChanges.From(PInvoke.WM_THEMECHANGED, 0)));
        Assert.IsFalse(changes.Add(SystemChanges.From(PInvoke.WM_SYSCOLORCHANGE, 0)));

        Assert.AreEqual(SystemChange.Screens | SystemChange.Theme, changes.Take());
    }

    [TestMethod]
    public void A_later_change_is_followed_again()
    {
        var changes = new SystemChanges();
        changes.Add(SystemChange.Theme);
        changes.Take();

        Assert.IsTrue(changes.Add(SystemChange.Theme));
        Assert.AreEqual(SystemChange.Theme, changes.Take());
        Assert.AreEqual(SystemChange.None, changes.Take());
    }

    [TestMethod]
    public void Each_broadcast_asks_for_what_it_changes()
    {
        Assert.AreEqual(SystemChange.Screens, SystemChanges.From(PInvoke.WM_DISPLAYCHANGE, 0));
        Assert.AreEqual(SystemChange.Screens | SystemChange.Theme, SystemChanges.From(PInvoke.WM_SETTINGCHANGE, 0));
        Assert.AreEqual(SystemChange.WorkArea | SystemChange.Theme,
            SystemChanges.From(PInvoke.WM_SETTINGCHANGE, (nint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWORKAREA));
        Assert.AreEqual(SystemChange.Theme, SystemChanges.From(PInvoke.WM_THEMECHANGED, 0));
        Assert.AreEqual(SystemChange.Theme, SystemChanges.From(PInvoke.WM_DWMCOLORIZATIONCOLORCHANGED, 0));
    }

    [TestMethod]
    public void An_unrelated_message_posts_nothing()
    {
        var changes = new SystemChanges();

        Assert.IsFalse(changes.Add(SystemChanges.From(PInvoke.WM_SIZE, 0)));
        Assert.IsTrue(changes.Add(SystemChanges.From(PInvoke.WM_SYSCOLORCHANGE, 0)));
    }
}
