using Winshell.Native;

namespace Winshell.Windows;

/// <summary>One virtual desktop, as a read of the shell reported it, before a display name is chosen.</summary>
internal readonly record struct DesktopRecord(Guid Id, string Name);

/// <summary>
/// Whether a Windows build uses the virtual desktop method table Winshell calls. Windows 11 23H2 shares some of
/// the same interface ids and a different table, so the build number is the guard, not the interface id. The
/// builds are <see cref="VirtualDesktopBuilds"/>.
/// </summary>
internal readonly record struct DesktopContract(bool Supported, string Reason)
{
    /// <summary>The shell's report that <paramref name="windows"/> is outside <see cref="VirtualDesktopBuilds"/>.</summary>
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

/// <summary>Decisions about virtual desktops that do not call Windows, so tests can lock them.</summary>
internal static class VirtualDesktopRules
{
    public static string FormatId(Guid id)
    {
        return id == Guid.Empty ? "" : id.ToString("D");
    }

    /// <summary>The name Windows stored, or <c>Desktop N</c> when it stored none. <paramref name="index"/> is zero-based.</summary>
    public static string DisplayName(string? name, int index)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim();
        return $"Desktop {index + 1}";
    }

    /// <summary>
    /// The desktop that receives a removed desktop's windows: the one before it, or the next when it is the first.
    /// Null when it is the only desktop, or the index is not in the list.
    /// </summary>
    public static int? FallbackIndex(int index, int count)
    {
        if (count <= 1 || index < 0 || index >= count)
            return null;
        return index == 0 ? 1 : index - 1;
    }
}
