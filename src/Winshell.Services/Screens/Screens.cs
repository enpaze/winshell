using Windows.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Winshell.Native;

namespace Winshell;

internal sealed record MonitorGeometry(nint Handle, string Id, RECT Bounds, RECT Work, double Scale, bool IsPrimary)
{
    public string Name => Id.Replace(@"\\.\", "", StringComparison.Ordinal);

    public RECT Area => Work is { Width: > 0, Height: > 0 } ? Work : Bounds;

    public Rect Logical => DesktopSpace.ToLogical(Bounds, Bounds, Scale);

    public MonitorFrame Frame => new(Area.left, Area.top, Area.right, Area.bottom, Scale);
}

internal sealed record ConnectedMonitor(nint Handle, ScreenInfo Info, string InterfacePath)
{
    public string ModelCode => Screens.ModelCode(InterfacePath);
}

internal static class Screens
{
    public static List<MonitorGeometry> List()
    {
        var result = new List<MonitorGeometry>();
        foreach (var handle in Monitors.List())
            if (Read(handle) is { } monitor)
                result.Add(monitor);
        return result.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.Bounds.left).ThenBy(m => m.Bounds.top)
            .ToList();
    }

    public static List<ConnectedMonitor> Enumerate()
    {
        return List().Select(Describe).ToList();
    }

    public static MonitorGeometry? Find(string deviceName)
    {
        return List().FirstOrDefault(m => m.Id == deviceName);
    }

    public static MonitorGeometry? Primary()
    {
        return List().FirstOrDefault();
    }

    private static MonitorGeometry? Read(nint handle)
    {
        if (!Monitors.TryRead(handle, out var mi))
            return null;
        PInvoke.GetDpiForMonitor((HMONITOR)handle, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpi, out _);
        return new MonitorGeometry(handle, mi.Device, mi.Bounds, mi.Work, DesktopSpace.Scale(dpi), mi.IsPrimary);
    }

    public static ConnectedMonitor Describe(MonitorGeometry monitor)
    {
        var device = monitor.Id;
        var (model, id) = DisplayDevice.Monitor(device);
        var size = MonitorSize.Read(id);
        var info = new ScreenInfo(
            device,
            monitor.Name,
            model,
            Manufacturer(ModelCode(id)),
            Serial(id),
            monitor.Logical,
            DesktopSpace.ToLogical(monitor.Work, monitor.Bounds, monitor.Scale),
            monitor.Scale,
            size.WidthMm,
            size.HeightMm,
            RefreshRate(device),
            monitor.IsPrimary);
        return new ConnectedMonitor(monitor.Handle, info, id);
    }

    public static MonitorGeometry? Of(nint hwnd)
    {
        return hwnd == 0
            ? null
            : Read(PInvoke.MonitorFromWindow((HWND)hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST));
    }

    internal static string ModelCode(string id)
    {
        var separator = id.Contains('#') ? '#' : '\\';
        var parts = id.Split(separator);
        return parts.Length > 1 ? parts[1] : "";
    }

    internal static string Manufacturer(string modelCode)
    {
        return modelCode.Length >= 3 ? modelCode[..3] : "";
    }

    internal static string Serial(string id)
    {
        var parts = id.Split('#');
        return parts.Length > 2 ? parts[2] : "";
    }

    private static int RefreshRate(string device)
    {
        return Monitors.RefreshRate(device) is { } hertz and > 1 ? (int)hertz : 60;
    }
}
