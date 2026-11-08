using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Winshell;

/// <summary>Pictures in memory as sources WinUI can show.</summary>
public static class PixelImageExtensions
{
    /// <summary>
    /// Makes a new source for a WinUI <c>Image</c>, with the picture's pixel size and colors.
    /// Call on the UI thread. The pixels are copied and premultiplied when needed; the picture is unchanged.
    /// </summary>
    /// <param name="image">The picture to show.</param>
    /// <returns>A source that keeps its own copy of the pixels.</returns>
    [PublicAPI]
    public static ImageSource ToImageSource(this PixelImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bitmap = new WriteableBitmap(image.Width, image.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            if (image.Premultiplied)
            {
                stream.Write(image.Bgra);
            }
            else
            {
                var pixels = image.Bgra.ToArray();
                for (var i = 0; i < pixels.Length; i += 4)
                {
                    var alpha = pixels[i + 3];
                    pixels[i] = (byte)(pixels[i] * alpha / 255);
                    pixels[i + 1] = (byte)(pixels[i + 1] * alpha / 255);
                    pixels[i + 2] = (byte)(pixels[i + 2] * alpha / 255);
                }

                stream.Write(pixels);
            }
        }

        bitmap.Invalidate();
        return bitmap;
    }
}
