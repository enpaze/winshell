using Windows.UI;

namespace OmarchyStyle;

internal static class TokyoNight
{
    public static ShellStyle Style { get; } = new()
    {
        Background = Color.FromArgb(0xff, 0x1a, 0x1b, 0x26),
        Foreground = Color.FromArgb(0xff, 0xa9, 0xb1, 0xd6),
        Alert = Color.FromArgb(0xff, 0xf7, 0x76, 0x8e),
        FontFamily = "ms-appx:///Fonts/JetBrainsMonoNerdFont-Regular.ttf#JetBrainsMono NF",
        FontSize = 12,
        SlotSize = 26,
        Padding = 8
    };
}
