using Winshell.Native;

namespace Winshell.Windows;

internal readonly record struct DesktopRecord(Guid Id, string Name);

internal readonly record struct DesktopContract(bool Supported, string Reason)
{
    public static string Unsupported(string windows)
    {
        return $"{windows} is outside the virtual desktop method table Winshell knows ({VirtualDesktopBuilds.OldestBuild} through {VirtualDesktopBuilds.NewestBuild}).";
    }

    public static DesktopContract ForBuild(uint build)
    {
        if (VirtualDesktopBuilds.Known(build))
            return new DesktopContract(true, "");
        return new DesktopContract(false, Unsupported($"Windows build {build}"));
    }
}

internal static class VirtualDesktopRules
{
    public static string FormatId(Guid id)
    {
        return id == Guid.Empty ? "" : id.ToString("D");
    }

    public static string DisplayName(string? name, int index)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim();
        return $"Desktop {index + 1}";
    }

    public static int? FallbackIndex(int index, int count)
    {
        if (count <= 1 || index < 0 || index >= count)
            return null;
        return index == 0 ? 1 : index - 1;
    }
}
