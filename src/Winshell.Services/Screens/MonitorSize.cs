using System.Collections.Concurrent;
using Windows.Devices.Display;

namespace Winshell;

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

    internal static (double WidthMm, double HeightMm) FromInches(double? widthInches, double? heightInches)
    {
        if (widthInches is not > 0 || heightInches is not > 0)
            return (0, 0);
        return (widthInches.Value * 25.4, heightInches.Value * 25.4);
    }
}

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
#pragma warning disable VSTHRD002
        return started.Result;
#pragma warning restore VSTHRD002
    }
}
