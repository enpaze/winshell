using System.Runtime.InteropServices;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Winshell.Native;

/// <summary>One screen's wallpaper, before it is a <c>WallpaperScreen</c>.</summary>
/// <param name="Id">The screen id, such as <c>\\.\DISPLAY1</c>, or the monitor id when it could not be matched.</param>
/// <param name="MonitorId">The id <c>IDesktopWallpaper</c> uses for this screen.</param>
/// <param name="Image">The image path, or empty when the screen shows only the background color.</param>
/// <param name="IsPrimary">Whether this is the primary screen.</param>
internal readonly record struct DesktopMonitor(string Id, string MonitorId, string Image, bool IsPrimary);

/// <summary>A reading of the desktop wallpaper. <paramref name="Position"/> is -1 when Windows did not say.</summary>
/// <param name="Available">Whether Explorer's wallpaper server answered.</param>
/// <param name="Error">The failure, or 0 when <paramref name="Available"/>.</param>
/// <param name="Monitors">Each screen, in the order Windows lists them.</param>
/// <param name="Position">A <see cref="DESKTOP_WALLPAPER_POSITION"/>, or -1.</param>
/// <param name="Color">The background color as a <c>COLORREF</c>.</param>
internal readonly record struct DesktopWallpaperState(
    bool Available,
    int Error,
    IReadOnlyList<DesktopMonitor> Monitors,
    int Position,
    uint Color);

/// <summary>
/// The desktop wallpaper through <c>IDesktopWallpaper</c>, the API Settings uses. It is a local COM server
/// (<c>CLSCTX_INPROC_SERVER</c> returns <c>REGDB_E_CLASSNOTREG</c>; the class is the AppId "Desktop Wallpaper
/// Factory", RunAs Interactive User). <c>CoCreateInstance</c> therefore fails when Explorer's desktop is not
/// running, and a reading then reports itself unavailable. <c>SystemParametersInfo</c> cannot give each screen its
/// own image, and its style numbers are not <see cref="DESKTOP_WALLPAPER_POSITION"/> (Fill is 4 here and 10 in
/// <c>WallpaperStyle</c>).
/// <para>
/// <c>GetWallpaper</c> for one monitor returns the image on that screen. An empty path is a solid color. Passing
/// null while the screens differ, or while a slideshow is running, returns <c>S_FALSE</c> and an empty string, so a
/// reading always asks for one monitor. A slideshow or Windows Spotlight is not a mode this service sets. The path
/// is whatever Windows reports for that screen: the slide it is showing, or the Spotlight image it has placed, or
/// empty when it reports none.
/// </para>
/// </summary>
internal static class DesktopWallpaper
{
    /// <summary><c>CLSID_DesktopWallpaper</c>.</summary>
    private static readonly Guid Clsid = new("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD");

    private const int NoInterface = unchecked((int)0x80004002);

    private const int InvalidArg = unchecked((int)0x80070057);

    private const int MaxMonitors = 64;

    /// <summary>Each screen's image, the fit and the background color. Unavailable when the server cannot be opened.</summary>
    internal static DesktopWallpaperState Read()
    {
        var desktop = Open(out var hr);
        if (desktop is null)
            return Unavailable(hr < 0 ? hr : NoInterface);
        try
        {
            var countHr = desktop.GetMonitorDevicePathCount(out var count);
            if (countHr < 0 || count > MaxMonitors)
                return Unavailable(countHr < 0 ? countHr : InvalidArg);
            var (byPath, byRect) = Displays();
            var monitors = new DesktopMonitor[count];
            for (uint i = 0; i < count; i++)
            {
                var idHr = desktop.GetMonitorDevicePathAt(i, out var idPointer);
                if (idHr < 0)
                    return Unavailable(idHr);
                var monitorId = Take(idPointer);
                var imageHr = desktop.GetWallpaper(monitorId, out var imagePointer);
                var image = imageHr >= 0 ? Take(imagePointer) : "";
                var known = Match(desktop, monitorId, byPath, byRect);
                monitors[i] = new DesktopMonitor(known.Id, monitorId, image, known.IsPrimary);
            }

            var position = desktop.GetPosition(out var fit) >= 0 ? (int)fit : -1;
            var color = desktop.GetBackgroundColor(out var background) >= 0 ? background : 0u;
            return new DesktopWallpaperState(true, 0, monitors, position, color);
        }
        catch (COMException e)
        {
            return Unavailable(e.HResult);
        }
        finally
        {
            Com.Release(desktop);
        }
    }

    /// <summary>
    /// Sets one screen's image, or every screen's when <paramref name="monitorId"/> is null. Returns a failure code,
    /// or 0.
    /// </summary>
    internal static int SetImage(string? monitorId, string image)
    {
        return Use(desktop => desktop.SetWallpaper(monitorId, image));
    }

    /// <summary>Sets the fit. Returns a failure code, or 0. A position Windows does not define is refused here.</summary>
    internal static int SetPosition(int position)
    {
        if (!Enum.IsDefined(typeof(DESKTOP_WALLPAPER_POSITION), position))
            return InvalidArg;
        return Use(desktop => desktop.SetPosition((DESKTOP_WALLPAPER_POSITION)position));
    }

    /// <summary>Sets the background color, a <c>COLORREF</c>. Returns a failure code, or 0.</summary>
    internal static int SetColor(uint color)
    {
        return Use(desktop => desktop.SetBackgroundColor(color));
    }

    private static DesktopWallpaperState Unavailable(int error)
    {
        return new DesktopWallpaperState(false, error, [], -1, 0);
    }

    private static (string Id, bool IsPrimary) Match(IDesktopWallpaper desktop, string monitorId,
        Dictionary<string, (string Id, bool IsPrimary)> byPath,
        Dictionary<(int Left, int Top, int Right, int Bottom), (string Id, bool IsPrimary)> byRect)
    {
        if (monitorId.Length > 0 && byPath.TryGetValue(monitorId, out var byName))
            return byName;
        if (monitorId.Length > 0 && desktop.GetMonitorRECT(monitorId, out var rect) >= 0 &&
            byRect.TryGetValue((rect.Left, rect.Top, rect.Right, rect.Bottom), out var byPlace))
            return byPlace;
        return (monitorId, false);
    }

    /// <summary>The connected screens, by the wallpaper monitor id and by the rectangle both APIs report.</summary>
    private static (Dictionary<string, (string Id, bool IsPrimary)> ByPath,
        Dictionary<(int Left, int Top, int Right, int Bottom), (string Id, bool IsPrimary)> ByRect) Displays()
    {
        var byPath = new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase);
        var byRect = new Dictionary<(int, int, int, int), (string, bool)>();
        foreach (var handle in Monitors.List())
        {
            if (!Monitors.TryRead(handle, out var info))
                continue;
            var (_, path) = DisplayDevice.Monitor(info.Device);
            if (path.Length > 0)
                byPath[path] = (info.Device, info.IsPrimary);
            byRect[(info.Bounds.Left, info.Bounds.Top, info.Bounds.Right, info.Bounds.Bottom)] =
                (info.Device, info.IsPrimary);
        }

        return (byPath, byRect);
    }

    private static int Use(Func<IDesktopWallpaper, int> call)
    {
        var desktop = Open(out var hr);
        if (desktop is null)
            return hr < 0 ? hr : NoInterface;
        try
        {
            return call(desktop);
        }
        catch (COMException e)
        {
            return e.HResult;
        }
        finally
        {
            Com.Release(desktop);
        }
    }

    private static IDesktopWallpaper? Open(out int hr)
    {
        return Com.Create<IDesktopWallpaper>(Clsid, CLSCTX.CLSCTX_ALL, out hr);
    }

    private static string Take(nint pointer)
    {
        if (pointer == 0)
            return "";
        try
        {
            return Marshal.PtrToStringUni(pointer) ?? "";
        }
        finally
        {
            Free(pointer);
        }
    }

    private static void Free(nint pointer)
    {
        if (pointer != 0)
            Marshal.FreeCoTaskMem(pointer);
    }

    /// <summary>
    /// <c>IDesktopWallpaper</c>, declared here. Generating it from the metadata pulls <c>IShellItemArray</c> and then
    /// <c>IMoniker</c>, whose friendly methods use the banned <c>ComTypes</c> interfaces. Only the methods up to the
    /// fit and the color are declared; their slots are the Windows order, and a slideshow is not set from here.
    /// </summary>
    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        [PreserveSig]
        int SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId,
            [MarshalAs(UnmanagedType.LPWStr)] string? image);

        [PreserveSig]
        int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, out nint image);

        [PreserveSig]
        int GetMonitorDevicePathAt(uint index, out nint monitorId);

        [PreserveSig]
        int GetMonitorDevicePathCount(out uint count);

        [PreserveSig]
        int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, out RECT bounds);

        [PreserveSig]
        int SetBackgroundColor(uint color);

        [PreserveSig]
        int GetBackgroundColor(out uint color);

        [PreserveSig]
        int SetPosition(DESKTOP_WALLPAPER_POSITION position);

        [PreserveSig]
        int GetPosition(out DESKTOP_WALLPAPER_POSITION position);
    }
}
