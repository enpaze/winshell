namespace Winshell.ShellTests;

public sealed class ColorSchemeTests
{
    [Fact]
    public void The_color_scheme_overrides_Windows_mode()
    {
        Assert.True(ShellPlatform.DarkFor(ColorScheme.Dark, false));
        Assert.False(ShellPlatform.DarkFor(ColorScheme.Light, true));
        Assert.True(ShellPlatform.DarkFor(ColorScheme.System, true));
        Assert.False(ShellPlatform.DarkFor(ColorScheme.System, false));
    }
}
