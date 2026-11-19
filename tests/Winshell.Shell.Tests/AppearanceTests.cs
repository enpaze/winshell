namespace Winshell.ShellTests;

public sealed class AppearanceTests
{
    [Fact]
    public void The_appearance_overrides_Windows_mode()
    {
        Assert.True(ShellPlatform.DarkFor(Appearance.Dark, false));
        Assert.False(ShellPlatform.DarkFor(Appearance.Light, true));
        Assert.True(ShellPlatform.DarkFor(Appearance.System, true));
        Assert.False(ShellPlatform.DarkFor(Appearance.System, false));
    }
}
