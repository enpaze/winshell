using Winshell.Native;

namespace Winshell;

public static class ShellImage
{
    public static PixelImage? Load(string shellPath, int size)
    {
        ArgumentNullException.ThrowIfNull(shellPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        return ShellItems.Icon(shellPath, size) is { } icon
            ? new PixelImage(icon.Width, icon.Height, icon.Bgra, true)
            : null;
    }
}
