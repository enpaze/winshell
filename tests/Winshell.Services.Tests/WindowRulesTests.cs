using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;
using Winshell.AppWindows;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class WindowRulesTests
{
    [TestMethod]
    public void App_identity_prefers_the_window_id_and_otherwise_uses_the_program_name()
    {
        Assert.AreEqual("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            WindowRules.AppId("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
                "", @"C:\Windows\System32\ApplicationFrameHost.exe"));
        Assert.AreEqual("chrome", WindowRules.AppId("", "", @"C:\Program Files\Google\Chrome\Application\chrome.exe"));
        Assert.AreEqual("Chrome", WindowRules.AppId("Chrome", "", @"C:\Program Files\Google\Chrome\Application\chrome.exe"));
        Assert.AreEqual("", WindowRules.AppId("", "", ""));
    }

    [TestMethod]
    [DataRow((uint)WINDOW_STYLE.WS_POPUP, 0, 0, 1920, 1080, true)]
    [DataRow((uint)(WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_THICKFRAME), -8, -8, 1928, 1088, true)]
    [DataRow((uint)(WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_THICKFRAME), -8, -8, 1928, 1088, false)]
    [DataRow((uint)WINDOW_STYLE.WS_POPUP, 0, 0, 1920, 1079, false)]
    [DataRow((uint)WINDOW_STYLE.WS_POPUP, 1, 0, 1920, 1080, false)]
    public void A_window_is_fullscreen_when_it_has_no_title_bar_and_covers_the_whole_monitor(
        uint style, int left, int top, int right, int bottom, bool fullscreen)
    {
        Assert.AreEqual(fullscreen,
            WindowRules.CoversMonitor(style, new RECT(left, top, right, bottom), new RECT(0, 0, 1920, 1080)));
    }

    [TestMethod]
    public void Foreground_and_top_level_events_mark_a_window_and_a_child_event_does_not()
    {
        Assert.IsTrue(WindowRules.MarksWindow(PInvoke.EVENT_SYSTEM_FOREGROUND, 1, 0, 0, false, false));
        Assert.IsFalse(WindowRules.MarksWindow(PInvoke.EVENT_SYSTEM_FOREGROUND, 0, 0, 0, false, false));
        Assert.IsTrue(WindowRules.MarksForeground(PInvoke.EVENT_SYSTEM_FOREGROUND));
        Assert.IsFalse(WindowRules.MarksWindow(PInvoke.EVENT_OBJECT_LOCATIONCHANGE, 1,
            (int)OBJECT_IDENTIFIER.OBJID_WINDOW, 1, true, true));
        Assert.IsFalse(WindowRules.MarksWindow(PInvoke.EVENT_OBJECT_LOCATIONCHANGE, 1,
            (int)OBJECT_IDENTIFIER.OBJID_WINDOW, (int)PInvoke.CHILDID_SELF, true, false));
        Assert.IsTrue(WindowRules.MarksWindow(PInvoke.EVENT_OBJECT_LOCATIONCHANGE, 1,
            (int)OBJECT_IDENTIFIER.OBJID_WINDOW, (int)PInvoke.CHILDID_SELF, false, true));
        Assert.IsFalse(WindowRules.MarksWindow(PInvoke.EVENT_OBJECT_DESTROY, 1, (int)OBJECT_IDENTIFIER.OBJID_WINDOW,
            (int)PInvoke.CHILDID_SELF, false, false));
        Assert.IsTrue(WindowRules.MarksWindow(PInvoke.EVENT_OBJECT_DESTROY, 1, (int)OBJECT_IDENTIFIER.OBJID_WINDOW,
            (int)PInvoke.CHILDID_SELF, true, false));
        Assert.IsTrue(WindowRules.MarksForeground(PInvoke.EVENT_SYSTEM_MINIMIZESTART));
        Assert.IsTrue(WindowRules.MarksForeground(PInvoke.EVENT_OBJECT_HIDE));
        Assert.IsFalse(WindowRules.MarksForeground(PInvoke.EVENT_OBJECT_NAMECHANGE));
    }

    [TestMethod]
    public void Caret_cursor_and_child_objects_are_not_window_events()
    {
        Assert.IsTrue(WindowRules.IsWindowObject((int)OBJECT_IDENTIFIER.OBJID_WINDOW, (int)PInvoke.CHILDID_SELF));
        Assert.IsFalse(WindowRules.IsWindowObject(-8, (int)PInvoke.CHILDID_SELF));
        Assert.IsFalse(WindowRules.IsWindowObject(-9, (int)PInvoke.CHILDID_SELF));
        Assert.IsFalse(WindowRules.IsWindowObject((int)OBJECT_IDENTIFIER.OBJID_WINDOW, 1));
    }

    [TestMethod]
    public void Only_HSHELL_FLASH_asks_for_attention()
    {
        Assert.IsTrue(WindowRules.IsFlash(0x8006));
        Assert.IsFalse(WindowRules.IsFlash(6));
        Assert.IsFalse(WindowRules.IsFlash(0x8004));
        Assert.IsFalse(WindowRules.IsFlash(1));
    }

    [TestMethod]
    public void A_window_wants_attention_until_it_is_activated_or_leaves_the_list()
    {
        HashSet<nint> wanting = [1, 2, 3];
        var listed = new HashSet<nint> { 1, 2 };
        WindowRules.SettleAttention(wanting, 1, listed.Contains);
        Assert.AreSequenceEqual([2], wanting);
        WindowRules.SettleAttention(wanting, 0, listed.Contains);
        Assert.AreSequenceEqual([2], wanting);
        WindowRules.SettleAttention(wanting, 2, listed.Contains);
        Assert.IsEmpty(wanting);
    }
}
