using System.Reflection;

namespace Winshell;

internal sealed record ShellProperties(
    string? ShellName = null,
    string? ShellDataDir = null,
    string? ShellStateDir = null,
    string? ShellCacheDir = null)
{
    public static ShellProperties Of(Assembly? program)
    {
        var metadata = (program?.GetCustomAttributes<AssemblyMetadataAttribute>() ?? [])
            .Where(attribute => attribute.Value is { Length: > 0 })
            .ToLookup(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
        return new ShellProperties(
            metadata[nameof(ShellName)].LastOrDefault(),
            metadata[nameof(ShellDataDir)].LastOrDefault(),
            metadata[nameof(ShellStateDir)].LastOrDefault(),
            metadata[nameof(ShellCacheDir)].LastOrDefault());
    }
}
