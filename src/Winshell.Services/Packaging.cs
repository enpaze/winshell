using Winshell.Native;

namespace Winshell;

internal static class Packaging
{
    public static bool IsPackaged { get; } = PackageIdentity.Exists();
}
