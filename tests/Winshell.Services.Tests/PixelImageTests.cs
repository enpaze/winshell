using Windows.Graphics.Imaging;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class PixelImageTests
{
    private static async Task<byte[]> DecodeAsync(byte[] png)
    {
        using var stream = new MemoryStream(png).AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.PngDecoderId, stream);
        Assert.AreEqual(2u, decoder.PixelWidth);
        Assert.AreEqual(1u, decoder.PixelHeight);
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        return pixels.DetachPixelData();
    }

    [TestMethod]
    public async Task A_png_keeps_the_size_and_the_pixels()
    {
        byte[] pixels = [10, 20, 30, 255, 40, 50, 60, 128];

        var png = new PixelImage(2, 1, pixels).EncodePng();

        Assert.AreSequenceEqual(pixels, await DecodeAsync(png));
    }

    [TestMethod]
    public async Task A_premultiplied_picture_is_saved_with_straight_alpha()
    {
        var png = new PixelImage(2, 1, [64, 32, 16, 128, 0, 0, 0, 0], true).EncodePng();

        Assert.AreSequenceEqual(new byte[] { 128, 64, 32, 128, 0, 0, 0, 0 }, await DecodeAsync(png));
    }

    [TestMethod]
    public async Task A_png_can_be_made_on_a_UI_thread()
    {
        byte[] pixels = [10, 20, 30, 255, 40, 50, 60, 128];
        byte[]? png = null;
        var thread = new Thread(() => png = new PixelImage(2, 1, pixels).EncodePng());
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        thread.Join();

        Assert.AreSequenceEqual(pixels, await DecodeAsync(png!));
    }

    [TestMethod]
    [DataRow(0, 1, 0)]
    [DataRow(1, 0, 0)]
    [DataRow(-1, -1, 4)]
    public void A_picture_has_a_size(int width, int height, int bytes)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PixelImage(width, height, new byte[bytes]));
    }

    [TestMethod]
    public void The_pixels_must_fill_the_picture()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new PixelImage(2, 2, new byte[15]));
    }
}
