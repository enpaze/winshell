using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Winshell.Native;

internal readonly record struct DesktopMonitor(string Id, string MonitorId, string Image, bool IsPrimary);

internal readonly record struct DesktopWallpaperState(
    bool Available,
    int Error,
    IReadOnlyList<DesktopMonitor> Monitors,
    int Position,
    uint Color);

internal static class DesktopWallpaper
{
    private static readonly Guid Clsid = new("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD");

    private const int NoInterface = unchecked((int)0x80004002);

    private const int InvalidArg = unchecked((int)0x80070057);

    private const int MaxMonitors = 64;

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

    internal static int SetImage(string? monitorId, string image)
    {
        return Use(desktop => desktop.SetWallpaper(monitorId, image));
    }

    internal static int SetPosition(int position)
    {
        if (!Enum.IsDefined(typeof(DESKTOP_WALLPAPER_POSITION), position))
            return InvalidArg;
        return Use(desktop => desktop.SetPosition((DESKTOP_WALLPAPER_POSITION)position));
    }

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
            byRect.TryGetValue((rect.left, rect.top, rect.right, rect.bottom), out var byPlace))
            return byPlace;
        return (monitorId, false);
    }

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
            byRect[(info.Bounds.left, info.Bounds.top, info.Bounds.right, info.Bounds.bottom)] =
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
            Marshal.FreeCoTaskMem(pointer);
        }
    }

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
