namespace Winshell.ShellTests;

[TestClass]
public sealed class AppearanceTests
{
    [TestMethod]
    public void The_appearance_overrides_Windows_mode()
    {
        Assert.IsTrue(ShellPlatform.DarkFor(Appearance.Dark, false));
        Assert.IsFalse(ShellPlatform.DarkFor(Appearance.Light, true));
        Assert.IsTrue(ShellPlatform.DarkFor(Appearance.System, true));
        Assert.IsFalse(ShellPlatform.DarkFor(Appearance.System, false));
    }
}
