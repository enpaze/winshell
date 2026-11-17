using Microsoft.UI.Xaml.Media;

namespace Winshell.ShellTests;

internal static class TestStyles
{
    public static ShellStyle Other() => new()
    {
        Background = Color(0x1a, 0x1b, 0x26),
        Foreground = Microsoft.UI.Colors.Crimson,
        Alert = Microsoft.UI.Colors.Orange,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 18,
        SlotSize = 40,
        Padding = null
    };

    public static ShellStyle Marked() => new()
    {
        Background = Microsoft.UI.Colors.Navy,
        Foreground = Microsoft.UI.Colors.White,
        Alert = Microsoft.UI.Colors.Gold,
        FontFamily = new FontFamily("Georgia"),
        FontSize = 15,
        SlotSize = 36,
        Padding = 2
    };

    private static global::Windows.UI.Color Color(byte red, byte green, byte blue) =>
        global::Windows.UI.Color.FromArgb(0xff, red, green, blue);
}
