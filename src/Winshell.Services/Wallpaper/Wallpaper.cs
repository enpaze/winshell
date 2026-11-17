namespace Winshell.Wallpaper;

public enum WallpaperFit
{
    Center = 0,

    Tile = 1,

    Stretch = 2,

    Fit = 3,

    Fill = 4,

    Span = 5
}

public readonly record struct WallpaperColor
{
    public WallpaperColor(byte red, byte green, byte blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    public byte Red { get; }

    public byte Green { get; }

    public byte Blue { get; }
}

public sealed class WallpaperScreen
{
    internal WallpaperScreen(string id, string monitorId, string image, bool isPrimary)
    {
        Id = id;
        MonitorId = monitorId;
        Image = image;
        IsPrimary = isPrimary;
    }

    public string Id { get; }

    public string Image { get; }

    public bool IsPrimary { get; }

    internal string MonitorId { get; }
}
