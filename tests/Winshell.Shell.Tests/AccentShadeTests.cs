using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Winshell.ShellTests;

[TestClass]
public sealed class AccentShadeTests
{
    [TestMethod]
    public void Shades_keep_the_accent_and_move_lightness_toward_white_and_black()
    {
        Assert.IsTrue(AccentShades.TryFrom("#0078D4", out var shades));
        Assert.AreEqual(0x00, shades.Accent.R);
        Assert.AreEqual(0x78, shades.Accent.G);
        Assert.AreEqual(0xD4, shades.Accent.B);
        Assert.IsTrue(Lightness(shades.Light1) > Lightness(shades.Accent));
        Assert.IsTrue(Lightness(shades.Light3) > Lightness(shades.Light1));
        Assert.IsTrue(Lightness(shades.Dark1) < Lightness(shades.Accent));
        Assert.IsTrue(Lightness(shades.Dark3) < Lightness(shades.Dark1));
        Assert.AreEqual("#0078D4", AccentShades.Normalize("#0078d4"));
        Assert.AreEqual("", AccentShades.Normalize("red"));
    }

    [TestMethod]
    public async Task A_picked_accent_repaints_the_brush_a_control_already_holds_and_following_Windows_gives_it_back()
    {
        var (windows, picked, followed) = await ShellFixture.On(shell =>
        {
            var controls = Application.Current.Resources.MergedDictionaries.OfType<XamlControlsResources>().Single();
            var brush =
                (SolidColorBrush)((ResourceDictionary)controls.ThemeDictionaries["Default"])["ToggleSwitchFillOn"];
            var before = brush.Color;
            shell.AccentColor = "#D13438";
            var after = brush.Color;
            shell.AccentColor = "";
            return (before, after, brush.Color);
        });

        Assert.IsTrue(AccentShades.TryFrom("#D13438", out var shades));
        Assert.Contains(picked, new[] { shades.Light2, shades.Dark1 });
        Assert.AreEqual(windows, followed);
    }

    private static double Lightness(Color color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        return (Math.Max(r, Math.Max(g, b)) + Math.Min(r, Math.Min(g, b))) / 2;
    }
}
