using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Winshell.ShellTests;

public sealed class AccentShadeTests
{
    private static readonly Color Red = Color.FromArgb(255, 0xD1, 0x34, 0x38);

    [Fact]
    public void Shades_keep_the_accent_and_move_lightness_toward_white_and_black()
    {
        var shades = AccentShades.From(Color.FromArgb(255, 0x00, 0x78, 0xD4));
        Assert.Equal(0x00, shades.Accent.R);
        Assert.Equal(0x78, shades.Accent.G);
        Assert.Equal(0xD4, shades.Accent.B);
        Assert.True(Lightness(shades.Light1) > Lightness(shades.Accent));
        Assert.True(Lightness(shades.Light3) > Lightness(shades.Light1));
        Assert.True(Lightness(shades.Dark1) < Lightness(shades.Accent));
        Assert.True(Lightness(shades.Dark3) < Lightness(shades.Dark1));
    }

    [Fact]
    public async Task A_picked_accent_repaints_the_brush_a_control_already_holds_and_following_Windows_gives_it_back()
    {
        var (windows, picked, followed) = await ShellFixture.On(shell =>
        {
            var controls = Application.Current.Resources.MergedDictionaries.OfType<XamlControlsResources>().Single();
            var brush =
                (SolidColorBrush)((ResourceDictionary)controls.ThemeDictionaries["Default"])["ToggleSwitchFillOn"];
            var before = brush.Color;
            shell.AccentColor = Red;
            var after = brush.Color;
            shell.AccentColor = null;
            return (before, after, brush.Color);
        });

        var shades = AccentShades.From(Red);
        Assert.Contains(picked, new[] { shades.Light2, shades.Dark1 });
        Assert.Equal(windows, followed);
    }

    [Fact]
    public async Task A_shell_follows_the_Windows_accent_until_it_picks_one()
    {
        var (picked, system) = await ShellFixture.On(shell => (shell.AccentColor, shell.SystemAccent));

        Assert.Null(picked);
        Assert.Equal(new UISettings().GetColorValue(UIColorType.Accent), system);
    }

    [Fact]
    public async Task Light_and_dark_controls_follow_the_Windows_accent_after_a_picked_one_is_cleared()
    {
        var blue = AccentShades.From(Color.FromArgb(255, 0x00, 0x63, 0xB1));
        var green = AccentShades.From(Color.FromArgb(255, 0x10, 0x7C, 0x10));
        var red = AccentShades.From(Red);

        var (picked, cleared, changed, system) = await ShellFixture.On(shell =>
        {
            var platform = shell.Platform;
            var windows = platform.WindowsAccent;
            try
            {
                platform.WindowsAccent = () => blue;
                shell.AccentColor = Red;
                platform.ReadTheme();
                var picked = Fills();
                shell.AccentColor = null;
                var cleared = Fills();
                platform.WindowsAccent = () => green;
                platform.ReadTheme();
                return (picked, cleared, Fills(), shell.SystemAccent);
            }
            finally
            {
                platform.WindowsAccent = windows;
                platform.ReadTheme();
            }
        });

        Assert.Equal((red.Light2, red.Dark1), picked);
        Assert.Equal((blue.Light2, blue.Dark1), cleared);
        Assert.Equal((green.Light2, green.Dark1), changed);
        Assert.Equal(green.Accent, system);
    }

    private static (Color Dark, Color Light) Fills()
    {
        var controls = Application.Current.Resources.MergedDictionaries.OfType<XamlControlsResources>().Single();
        Color Fill(string theme) =>
            ((SolidColorBrush)((ResourceDictionary)controls.ThemeDictionaries[theme])["AccentFillColorDefaultBrush"])
            .Color;
        return (Fill("Default"), Fill("Light"));
    }

    private static double Lightness(Color color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        return (Math.Max(r, Math.Max(g, b)) + Math.Min(r, Math.Min(g, b))) / 2;
    }
}
