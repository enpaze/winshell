namespace Winshell.Wallpaper;

/// <summary>How the wallpaper image is fitted to the screen. Every screen uses the same fit.</summary>
public enum WallpaperFit
{
    /// <summary>The image keeps its size and sits in the middle of the screen.</summary>
    Center = 0,

    /// <summary>The image repeats until it covers the screen.</summary>
    Tile = 1,

    /// <summary>The image is stretched to the screen, even when that changes its shape.</summary>
    Stretch = 2,

    /// <summary>The whole image is shown, as large as it can be without cropping or changing its shape.</summary>
    Fit = 3,

    /// <summary>The image covers the screen without changing its shape, cropping whatever does not fit.</summary>
    Fill = 4,

    /// <summary>One image covers every screen, as large as it can be without changing its shape.</summary>
    Span = 5
}

/// <summary>The color behind the wallpaper image, and the color of a screen that has no image.</summary>
public readonly record struct WallpaperColor
{
    /// <summary>A color of <paramref name="red"/>, <paramref name="green"/> and <paramref name="blue"/>, each from 0 to 255.</summary>
    public WallpaperColor(byte red, byte green, byte blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>Red, from 0 to 255.</summary>
    public byte Red { get; }

    /// <summary>Green, from 0 to 255.</summary>
    public byte Green { get; }

    /// <summary>Blue, from 0 to 255.</summary>
    public byte Blue { get; }
}

/// <summary>The wallpaper on one screen.</summary>
public sealed class WallpaperScreen
{
    internal WallpaperScreen(string id, string monitorId, string image, bool isPrimary)
    {
        Id = id;
        MonitorId = monitorId;
        Image = image;
        IsPrimary = isPrimary;
    }

    /// <summary>
    /// The screen's id, the same one <see cref="ScreenInfo"/> uses, such as <c>\\.\DISPLAY1</c>. A monitor that
    /// could not be matched keeps the id Windows uses for its wallpaper.
    /// </summary>
    public string Id { get; }

    /// <summary>The image on this screen, or empty when the screen shows only the background color.</summary>
    public string Image { get; }

    /// <summary>Whether this is the primary screen.</summary>
    public bool IsPrimary { get; }

    /// <summary>The id <c>IDesktopWallpaper</c> uses. Empty when Windows named none.</summary>
    internal string MonitorId { get; }
}
