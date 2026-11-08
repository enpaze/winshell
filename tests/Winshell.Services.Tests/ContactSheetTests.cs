using Winshell.Capture;

namespace Winshell.Services.Tests;

/// <summary>A recording's contact sheet: every frame in a numbered grid, read like text.</summary>
public sealed class ContactSheetTests
{
    private static PixelImage Solid(int width, int height, byte blue, byte green, byte red)
    {
        var bgra = new byte[width * height * 4];
        for (var i = 0; i < bgra.Length; i += 4)
            (bgra[i], bgra[i + 1], bgra[i + 2], bgra[i + 3]) = (blue, green, red, 255);
        return new PixelImage(width, height, bgra, true);
    }

    [Theory]
    [InlineData(1, 2560, 1600)]
    [InlineData(10, 2560, 1600)]
    [InlineData(300, 2560, 1600)]
    [InlineData(7200, 2560, 1600)]
    [InlineData(100, 2560, 48)]
    [InlineData(7, 300, 900)]
    [InlineData(3, 40, 20)]
    public void Every_cell_and_its_label_fit_inside_the_sheet(int count, int width, int height)
    {
        var layout = ContactSheet.Layout(count, width, height);

        foreach (var index in (int[])[layout.Columns - 1, count - 1])
        {
            var (x, y) = layout.Cell(index);
            Assert.InRange(x + layout.CellWidth, 1, layout.Width);
            Assert.InRange(y + layout.ThumbHeight + ContactSheet.LabelHeight, 1, layout.Height);
        }
    }

    [Fact]
    public void Thousands_of_frames_get_smaller_thumbnails()
    {
        var few = ContactSheet.Layout(10, 2560, 1600);
        var many = ContactSheet.Layout(7200, 2560, 1600);

        Assert.True(many.ThumbWidth < few.ThumbWidth);
        Assert.True((long)many.Width * many.Height * 4 < int.MaxValue);
    }

    [Theory]
    [InlineData(2560, 1600)]
    [InlineData(2560, 48)]
    [InlineData(300, 900)]
    [InlineData(40, 20)]
    public void A_thumbnail_keeps_the_frame_s_shape_and_is_never_bigger(int width, int height)
    {
        var layout = ContactSheet.Layout(4, width, height);

        Assert.InRange(layout.ThumbWidth, 1, width);
        Assert.InRange(layout.ThumbHeight, 1, height);
        Assert.Equal((double)width / height, (double)layout.ThumbWidth / layout.ThumbHeight, 0.05 * width / height);
    }

    [Theory]
    [InlineData(300, 2560, 1600)]
    [InlineData(100, 2560, 48)]
    public void A_long_recording_makes_a_sheet_about_as_wide_as_it_is_tall(int count, int width, int height)
    {
        var layout = ContactSheet.Layout(count, width, height);

        Assert.InRange((double)layout.Width / layout.Height, 0.5, 2);
    }

    [Fact]
    public void The_frames_are_drawn_whole_in_the_order_they_were_captured_with_their_labels()
    {
        PixelImage[] frames = [Solid(400, 300, 0, 0, 255), Solid(400, 300, 0, 255, 0), Solid(400, 300, 255, 0, 0)];

        var sheet = ContactSheet.Render(
            [.. frames.Select((frame, i) => (frame, TimeSpan.FromMilliseconds(i * 16)))]);

        // Read like text, a row at a time: where each color first shows, and how much of it there is.
        var first = new int[3];
        var area = new int[3];
        var labels = 0;
        var pixels = sheet.Bgra;
        for (var i = pixels.Length - 4; i >= 0; i -= 4)
        {
            var color = (pixels[i], pixels[i + 1], pixels[i + 2]) switch
            {
                (0, 0, 255) => 0,
                (0, 255, 0) => 1,
                (255, 0, 0) => 2,
                (224, 224, 224) => 3,
                _ => -1
            };
            if (color == 3)
                labels++;
            else if (color >= 0)
                (first[color], area[color]) = (i, area[color] + 1);
        }

        Assert.True(first[0] < first[1] && first[1] < first[2]);
        Assert.True(area[0] > 0);
        Assert.Equal(area[0], area[1]);
        Assert.Equal(area[0], area[2]);
        Assert.True(labels > 0);
    }
}
