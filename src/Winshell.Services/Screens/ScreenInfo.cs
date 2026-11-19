using Windows.Foundation;

namespace Winshell;

public sealed record ScreenInfo
{
    internal ScreenInfo(string id, string name, string model, string manufacturer, string serialNumber, Rect geometry,
        Rect workArea, double devicePixelRatio, double physicalWidthMm, double physicalHeightMm, int refreshRate,
        bool isPrimary)
    {
        Id = id;
        Name = name;
        Model = model;
        Manufacturer = manufacturer;
        SerialNumber = serialNumber;
        Geometry = geometry;
        WorkArea = workArea;
        DevicePixelRatio = devicePixelRatio;
        PhysicalWidthMm = physicalWidthMm;
        PhysicalHeightMm = physicalHeightMm;
        RefreshRate = refreshRate;
        IsPrimary = isPrimary;
    }

    public string Id { get; internal init; }

    public string Name { get; internal init; }

    public string Model { get; internal init; }

    public string Manufacturer { get; internal init; }

    public string SerialNumber { get; internal init; }

    public Rect Geometry { get; internal init; }

    public Rect WorkArea { get; internal init; }

    public double DevicePixelRatio { get; internal init; }

    public double PhysicalWidthMm { get; internal init; }

    public double PhysicalHeightMm { get; internal init; }

    public int RefreshRate { get; internal init; }

    public bool IsPrimary { get; internal init; }
}
