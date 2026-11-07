using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Winshell;

namespace OmarchyStyle;

/// <summary>
/// A tray icon as something an Image can show. Winshell has no conversion of its own yet
/// (https://github.com/enpaze/winshell/issues/1695), so this copies the pixels by hand.
/// </summary>
internal static class TrayIcons
{
    public static WriteableBitmap ToBitmap(PixelImage image)
    {
        // WinUI draws premultiplied BGRA.
        var pixels = image.Bgra.ToArray();
        if (!image.Premultiplied)
        {
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3];
                pixels[i] = (byte)(pixels[i] * alpha / 255);
                pixels[i + 1] = (byte)(pixels[i + 1] * alpha / 255);
                pixels[i + 2] = (byte)(pixels[i + 2] * alpha / 255);
            }
        }

        var bitmap = new WriteableBitmap(image.Width, image.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
            stream.Write(pixels);
        bitmap.Invalidate();
        return bitmap;
    }
}
