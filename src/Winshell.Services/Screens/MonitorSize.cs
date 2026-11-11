using System.Collections.Concurrent;
using Windows.Devices.Display;

namespace Winshell;

/// <summary>
/// A monitor's physical size in millimeters, read with the screen snapshot. The size is the EDID size from
/// <see cref="DisplayMonitor.PhysicalSizeInInches"/>. A projector or a remote session reports none, and then this is 0.
/// </summary>
internal static class MonitorSize
{
    private static readonly MonitorSizeCache Cache = new(Query);

    internal static (double WidthMm, double HeightMm) Read(string interfaceId)
    {
        return Cache.Read(interfaceId);
    }

    private static async Task<(double WidthMm, double HeightMm)> Query(string interfaceId)
    {
        try
        {
            var monitor = await DisplayMonitor.FromInterfaceIdAsync(interfaceId).AsTask().ConfigureAwait(false);
            var size = monitor?.PhysicalSizeInInches;
            return FromInches(size?.Width, size?.Height);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return (0, 0);
        }
    }

    /// <summary>Inches to millimeters. Either side missing or not positive is no size, so both are 0.</summary>
    internal static (double WidthMm, double HeightMm) FromInches(double? widthInches, double? heightInches)
    {
        if (widthInches is not > 0 || heightInches is not > 0)
            return (0, 0);
        return (widthInches.Value * 25.4, heightInches.Value * 25.4);
    }
}

/// <summary>
/// Asks Windows once for each monitor. A monitor is described each time the screen list is read or one screen is
/// looked up, often on the loop. The read never waits: a query that has not answered is 0, and a later read fills
/// the size in.
/// </summary>
internal sealed class MonitorSizeCache(Func<string, Task<(double WidthMm, double HeightMm)>> query)
{
    private readonly ConcurrentDictionary<string, Task<(double WidthMm, double HeightMm)>> _queries =
        new(StringComparer.Ordinal);

    internal (double WidthMm, double HeightMm) Read(string interfaceId)
    {
        if (string.IsNullOrEmpty(interfaceId))
            return (0, 0);
        var started = _queries.GetOrAdd(interfaceId, query);
        if (!started.IsCompletedSuccessfully)
            return (0, 0);
#pragma warning disable VSTHRD002 // The task has already completed, so this reads the cached size and does not wait.
        return started.Result;
#pragma warning restore VSTHRD002
    }
}
