namespace Winshell.Native;

internal static class VirtualDesktopBuilds
{
    internal const uint OldestBuild = 26100;

    internal const uint NewestBuild = 26399;

    internal static bool Known(uint? build)
    {
        return build is >= OldestBuild and <= NewestBuild;
    }
}
