using Windows.Foundation;

namespace Winshell;

public sealed record ScreenInfo(
    string Id,
    string Name,
    string Model,
    string Manufacturer,
    string SerialNumber,
    Rect Geometry,
    Rect WorkArea,
    double DevicePixelRatio,
    double PhysicalWidthMm,
    double PhysicalHeightMm,
    int RefreshRate,
    bool IsPrimary);
