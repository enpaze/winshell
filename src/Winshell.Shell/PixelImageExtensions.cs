using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Winshell;

public static class PixelImageExtensions
{
    public static ImageSource ToImageSource(this PixelImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bitmap = new WriteableBitmap(image.Width, image.Height);
        image.WriteInto(bitmap);
        return bitmap;
    }

    internal static void WriteInto(this PixelImage image, WriteableBitmap bitmap)
    {
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
    }
}
