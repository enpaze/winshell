namespace Winshell.Capture;

internal sealed record SheetLayout(
    int Columns,
    int Rows,
    int ThumbWidth,
    int ThumbHeight,
    int CellWidth,
    int Width,
    int Height)
{
    public (int X, int Y) Cell(int index)
    {
        return (ContactSheet.Gap + index % Columns * (CellWidth + ContactSheet.Gap),
            ContactSheet.Gap + index / Columns * (ThumbHeight + ContactSheet.LabelHeight + ContactSheet.Gap));
    }
}

internal static class ContactSheet
{
    internal const int Gap = 8;
    internal const int LabelHeight = GlyphHeight * Scale + 6;

    private const int ThumbPixels = 320 * 200;

    private const int MaxThumbWidth = 1280;

    private const int SheetPixels = 24_000_000;

    private const int MinCellWidth = 15 * (GlyphWidth + 1) * Scale;

    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private const int Scale = 2;
    private const byte Back = 32;
    private const byte Ink = 224;

    public static SheetLayout Layout(int count, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var pixels = Math.Min(ThumbPixels, SheetPixels / count);
        var scale = Math.Min(1, Math.Min(Math.Sqrt(pixels / ((double)width * height)),
            (double)MaxThumbWidth / width));
        var thumbWidth = Math.Max(1, (int)Math.Round(width * scale));
        var thumbHeight = Math.Max(1, (int)Math.Round(height * scale));
        var cellWidth = Math.Max(thumbWidth, MinCellWidth);
        var cellHeight = thumbHeight + LabelHeight;
        var columns = Math.Clamp((int)Math.Ceiling(Math.Sqrt((double)count * cellHeight / cellWidth)), 1, count);
        var rows = (count + columns - 1) / columns;
        return new SheetLayout(columns, rows, thumbWidth, thumbHeight, cellWidth,
            Gap + columns * (cellWidth + Gap), Gap + rows * (cellHeight + Gap));
    }

    public static PixelImage Render(IReadOnlyList<(PixelImage Frame, TimeSpan At)> frames)
    {
        ArgumentOutOfRangeException.ThrowIfZero(frames.Count);
        var layout = Layout(frames.Count, frames[0].Frame.Width, frames[0].Frame.Height);
        var sheet = new byte[checked(layout.Width * layout.Height * 4)];
        for (var i = 0; i < sheet.Length; i += 4)
        {
            sheet[i] = sheet[i + 1] = sheet[i + 2] = Back;
            sheet[i + 3] = 255;
        }

        for (var i = 0; i < frames.Count; i++)
        {
            var (frame, at) = frames[i];
            var (x, y) = layout.Cell(i);
            var fit = Math.Min((double)layout.ThumbWidth / frame.Width, (double)layout.ThumbHeight / frame.Height);
            var thumb = Scaled(frame, Math.Max(1, (int)(frame.Width * fit)), Math.Max(1, (int)(frame.Height * fit)));
            Draw(sheet, layout.Width, thumb, x, y);
            Write(sheet, layout.Width, $"#{i + 1} {Milliseconds(at)} ms", x, y + layout.ThumbHeight + 3);
        }

        return new PixelImage(layout.Width, layout.Height, sheet);
    }

    public static long Milliseconds(TimeSpan at)
    {
        return at.Ticks / TimeSpan.TicksPerMillisecond;
    }

    public static PixelImage Thumbnail(PixelImage image)
    {
        var layout = Layout(1, image.Width, image.Height);
        return Scaled(image, layout.ThumbWidth, layout.ThumbHeight);
    }

    private static PixelImage Scaled(PixelImage image, int width, int height)
    {
        width = Math.Min(width, image.Width);
        height = Math.Min(height, image.Height);
        if (width == image.Width && height == image.Height)
            return image;
        var source = image.Bgra;
        var scaled = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var top = (int)((long)y * image.Height / height);
            var bottom = Math.Max(top + 1, (int)((long)(y + 1) * image.Height / height));
            for (var x = 0; x < width; x++)
            {
                var left = (int)((long)x * image.Width / width);
                var right = Math.Max(left + 1, (int)((long)(x + 1) * image.Width / width));
                long b = 0, g = 0, r = 0, a = 0;
                for (var sy = top; sy < bottom; sy++)
                    for (var sx = left; sx < right; sx++)
                    {
                        var at = (sy * image.Width + sx) * 4;
                        b += source[at];
                        g += source[at + 1];
                        r += source[at + 2];
                        a += source[at + 3];
                    }

                var count = (bottom - top) * (right - left);
                var to = (y * width + x) * 4;
                scaled[to] = (byte)(b / count);
                scaled[to + 1] = (byte)(g / count);
                scaled[to + 2] = (byte)(r / count);
                scaled[to + 3] = (byte)(a / count);
            }
        }

        return new PixelImage(width, height, scaled, image.Premultiplied);
    }

    private static void Draw(byte[] sheet, int sheetWidth, PixelImage image, int x, int y)
    {
        var pixels = image.Bgra;
        for (var row = 0; row < image.Height; row++)
            for (var column = 0; column < image.Width; column++)
            {
                var from = (row * image.Width + column) * 4;
                var to = ((y + row) * sheetWidth + x + column) * 4;
                var alpha = pixels[from + 3];
                for (var channel = 0; channel < 3; channel++)
                {
                    var color = image.Premultiplied ? pixels[from + channel] : pixels[from + channel] * alpha / 255;
                    sheet[to + channel] = (byte)Math.Min(255, color + sheet[to + channel] * (255 - alpha) / 255);
                }
            }
    }

    private static void Write(byte[] sheet, int sheetWidth, string text, int x, int y)
    {
        foreach (var letter in text)
        {
            var glyph = Glyphs[letter];
            for (var row = 0; row < GlyphHeight; row++)
                for (var column = 0; column < GlyphWidth; column++)
                {
                    if (glyph[row][column] != '#')
                        continue;
                    for (var dy = 0; dy < Scale; dy++)
                        for (var dx = 0; dx < Scale && x + column * Scale + dx < sheetWidth; dx++)
                        {
                            var to = ((y + row * Scale + dy) * sheetWidth + x + column * Scale + dx) * 4;
                            sheet[to] = sheet[to + 1] = sheet[to + 2] = Ink;
                        }
                }

            x += (GlyphWidth + 1) * Scale;
        }
    }

    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        [' '] = [".....", ".....", ".....", ".....", ".....", ".....", "....."],
        ['#'] = [".#.#.", ".#.#.", "#####", ".#.#.", "#####", ".#.#.", ".#.#."],
        ['m'] = [".....", ".....", "##.#.", "#.#.#", "#.#.#", "#.#.#", "#.#.#"],
        ['s'] = [".....", ".....", ".####", "#....", ".###.", "....#", "####."],
        ['0'] = [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
        ['1'] = ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
        ['2'] = [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
        ['3'] = ["####.", "....#", "....#", ".###.", "....#", "....#", "####."],
        ['4'] = ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
        ['5'] = ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
        ['6'] = ["..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###."],
        ['7'] = ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
        ['8'] = [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
        ['9'] = [".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.."]
    };
}
