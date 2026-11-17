using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal static unsafe class IconBitmap
{
    internal readonly record struct Source(string Module, ushort Id, string Name);

    internal static Source? SourceOf(nint icon)
    {
        var info = new ICONINFOEXW { cbSize = (uint)sizeof(ICONINFOEXW) };
        if (icon == 0 || !PInvoke.GetIconInfoEx((HICON)icon, &info))
            return null;
        DeleteBitmaps(info.hbmColor, info.hbmMask);
        return new Source(info.szModName.ToString(), info.wResID, info.szResName.ToString());
    }

    internal static (int Size, byte[] Bgra)? Draw(nint icon, int size)
    {
        ICONINFO info;
        if (icon == 0 || !PInvoke.GetIconInfo((HICON)icon, &info))
            return null;
        try
        {
            if (size <= 0)
            {
                size = 32;
                BITMAP bmp;
                if (PInvoke.GetObject(info.hbmColor != default ? info.hbmColor : info.hbmMask, sizeof(BITMAP),
                        &bmp) != 0)
                    size = Math.Clamp(bmp.bmWidth, 16, 256);
            }

            return Pixels((HICON)icon, size) is { } bgra ? (size, bgra) : null;
        }
        finally
        {
            DeleteBitmaps(info.hbmColor, info.hbmMask);
        }
    }

    private static byte[]? Pixels(HICON icon, int size)
    {
        var header = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = size,
                biHeight = -size,
                biPlanes = 1,
                biBitCount = 32
            }
        };
        var dc = PInvoke.CreateCompatibleDC();
        if (dc == default)
            return null;
        HBITMAP dib = default;
        try
        {
            void* bits;
            dib = PInvoke.CreateDIBSection(dc, &header, DIB_USAGE.DIB_RGB_COLORS, &bits, default, 0);
            if (dib == default)
                return null;
            var old = PInvoke.SelectObject(dc, dib);
            PInvoke.DrawIconEx(dc, 0, 0, icon, size, size, 0, default, DI_FLAGS.DI_NORMAL);
            var pixels = new Span<uint>(bits, size * size);
            var hasAlpha = false;
            foreach (var p in pixels)
                if ((p & 0xff000000) != 0)
                {
                    hasAlpha = true;
                    break;
                }

            if (!hasAlpha)
            {
                var mask = new uint[size * size];
                PInvoke.DrawIconEx(dc, 0, 0, icon, size, size, 0, default, DI_FLAGS.DI_MASK);
                pixels.CopyTo(mask);
                PInvoke.DrawIconEx(dc, 0, 0, icon, size, size, 0, default, DI_FLAGS.DI_NORMAL);
                for (var i = 0; i < pixels.Length; i++)
                    pixels[i] = (mask[i] & 0xffffff) == 0 ? pixels[i] | 0xff000000 : 0;
            }

            var bgra = new Span<byte>(bits, size * size * 4).ToArray();
            PInvoke.SelectObject(dc, old);
            return bgra;
        }
        finally
        {
            PInvoke.DeleteDC(dc);
            if (dib != default)
                PInvoke.DeleteObject(dib);
        }
    }

    private static void DeleteBitmaps(HBITMAP color, HBITMAP mask)
    {
        if (color != default)
            PInvoke.DeleteObject(color);
        if (mask != default)
            PInvoke.DeleteObject(mask);
    }
}
