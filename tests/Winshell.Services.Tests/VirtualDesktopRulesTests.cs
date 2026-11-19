using Winshell.AppWindows;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class VirtualDesktopRulesTests
{
    [TestMethod]
    public void A_blank_desktop_name_becomes_Desktop_N_and_a_stored_name_is_kept()
    {
        Assert.AreEqual("Desktop 1", VirtualDesktopRules.DisplayName("  ", 0));
        Assert.AreEqual("Desktop 2", VirtualDesktopRules.DisplayName(null, 1));
        Assert.AreEqual("Work", VirtualDesktopRules.DisplayName("  Work  ", 3));
        Assert.AreEqual("", VirtualDesktopRules.FormatId(Guid.Empty));
        Assert.AreEqual("00112233-4455-6677-8899-aabbccddeeff",
            VirtualDesktopRules.FormatId(new Guid("00112233-4455-6677-8899-aabbccddeeff")));
    }

    [TestMethod]
    public void Removing_a_desktop_falls_back_to_the_previous_one_and_never_the_last()
    {
        Assert.AreEqual(1, VirtualDesktopRules.FallbackIndex(0, 3));
        Assert.AreEqual(0, VirtualDesktopRules.FallbackIndex(1, 3));
        Assert.AreEqual(1, VirtualDesktopRules.FallbackIndex(2, 3));
        Assert.IsNull(VirtualDesktopRules.FallbackIndex(0, 1));
        Assert.IsNull(VirtualDesktopRules.FallbackIndex(3, 3));
        Assert.IsNull(VirtualDesktopRules.FallbackIndex(-1, 2));
    }
}
