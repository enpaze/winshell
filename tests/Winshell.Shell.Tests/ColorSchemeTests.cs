namespace Winshell.ShellTests;

[TestClass]
public sealed class ColorSchemeTests
{
    [TestMethod]
    public void The_color_scheme_overrides_Windows_mode()
    {
        Assert.IsTrue(ShellPlatform.DarkFor(ColorScheme.Dark, false));
        Assert.IsFalse(ShellPlatform.DarkFor(ColorScheme.Light, true));
        Assert.IsTrue(ShellPlatform.DarkFor(ColorScheme.System, true));
        Assert.IsFalse(ShellPlatform.DarkFor(ColorScheme.System, false));
    }
}
