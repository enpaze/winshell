using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Winshell.ShellTests;

public sealed class PixelImageSourceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_pixel_image_source_shows_its_colors_without_changing_the_picture(bool premultiplied)
    {
        byte[] original = premultiplied
            ? [16, 32, 64, 128, 10, 20, 30, 255, 0, 0, 0, 0, 32, 16, 8, 64]
            : [32, 64, 128, 128, 10, 20, 30, 255, 100, 150, 200, 0, 128, 64, 32, 64];
        var picture = new PixelImage(2, 2, (byte[])original.Clone(), premultiplied);

        var shown = await ShellFixture.On(async shell =>
        {
            var image = new Image
            {
                Width = 2,
                Height = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Source = picture.ToImageSource()
            };
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            image.Loaded += (_, _) => loaded.TrySetResult();
            var screen = shell.Screens[0].Geometry;
            using var popup = new PopupWindow
            {
                Rect = new Rect(screen.X + 40, screen.Y + 40, 80, 80),
                Content = new Grid { Children = { image } }
            };
            popup.Visible = true;
            await loaded.Task.WaitAsync(TestContext.Current.CancellationToken);
            // Loaded precedes composition. Capture only once the visual has reached a render pass.
            var composing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            void Rendering(object? sender, object args)
            {
                composing.TrySetResult();
            }

            CompositionTarget.Rendering += Rendering;
            try
            {
                await composing.Task.WaitAsync(TestContext.Current.CancellationToken);
            }
            finally
            {
                CompositionTarget.Rendering -= Rendering;
            }

            var rendered = new RenderTargetBitmap();
            await rendered.RenderAsync(image, 2, 2).AsTask(TestContext.Current.CancellationToken);
            var buffer = await rendered.GetPixelsAsync().AsTask(TestContext.Current.CancellationToken);
            return (rendered.PixelWidth, rendered.PixelHeight, Pixels: buffer.ToArray());
        });

        Assert.Equal((2, 2), (shown.PixelWidth, shown.PixelHeight));
        Assert.Equal(new byte[] { 16, 32, 64, 128, 10, 20, 30, 255, 0, 0, 0, 0, 32, 16, 8, 64 }, shown.Pixels);
        Assert.Equal(original, picture.Bgra.ToArray());
    }
}
