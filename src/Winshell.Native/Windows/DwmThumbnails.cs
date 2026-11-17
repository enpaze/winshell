using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Winshell.Native;

internal static class DwmThumbnails
{
    internal static nint Register(nint destination, nint source)
    {
        return PInvoke.DwmRegisterThumbnail((HWND)destination, (HWND)source, out var thumbnail).Succeeded
            ? thumbnail
            : 0;
    }

    internal static bool TrySourceSize(nint thumbnail, out int width, out int height)
    {
        var ok = PInvoke.DwmQueryThumbnailSourceSize(thumbnail, out var size).Succeeded;
        (width, height) = ok ? (size.cx, size.cy) : (0, 0);
        return ok;
    }

    internal static bool Update(nint thumbnail, RECT destination, RECT source, byte opacity, bool visible)
    {
        var properties = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = PInvoke.DWM_TNP_RECTDESTINATION | PInvoke.DWM_TNP_RECTSOURCE | PInvoke.DWM_TNP_OPACITY |
                      PInvoke.DWM_TNP_VISIBLE,
            rcDestination = destination,
            rcSource = source,
            opacity = opacity,
            fVisible = visible
        };
        return PInvoke.DwmUpdateThumbnailProperties(thumbnail, properties).Succeeded;
    }

    internal static void Unregister(nint thumbnail)
    {
        _ = PInvoke.DwmUnregisterThumbnail(thumbnail);
    }
}
