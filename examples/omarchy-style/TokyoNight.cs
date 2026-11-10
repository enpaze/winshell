using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Winshell;

namespace OmarchyStyle;

/// <summary>Omarchy's Tokyo Night colors, the bundled Nerd Font, and a 26-pixel slot. AppShell sets it as the shell style.</summary>
internal static class TokyoNight
{
    public static ShellStyle Style { get; } = new()
    {
        Background = Color.FromArgb(0xff, 0x1a, 0x1b, 0x26),
        Foreground = Color.FromArgb(0xff, 0xa9, 0xb1, 0xd6),
        Alert = Color.FromArgb(0xff, 0xf7, 0x76, 0x8e),
        // The font file ships next to the program, so it needs no install.
        FontFamily = new("ms-appx:///Fonts/JetBrainsMonoNerdFont-Regular.ttf#JetBrainsMono NF"),
        FontSize = 12,
        // The height of a top or bottom bar, and the width of a left or right one.
        SlotSize = 26,
        // The space between the widgets and each end of a bar.
        Padding = 8
    };
}
