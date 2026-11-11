using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Winshell.Session;

/// <summary>
/// The sign-in entry as Windows has it, read without changing anything: an unpackaged Winshell's value under the
/// user's <c>Run</c> key, the on and off Startup apps keeps for it, and a packaged Winshell's startup task.
/// <see cref="WindowsStartupRegistry"/> and <see cref="WindowsStartupTasks"/> build on these reads, and the doctor
/// calls them as they are. A registry read can throw; the caller decides what that means.
/// </summary>
internal static class SignInEntry
{
    /// <summary>The name of the <c>Run</c> value, and the package's startup task id.</summary>
    public const string Name = "Winshell";

    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Where the Startup apps page keeps its on and off, one binary value per Run value.</summary>
    public const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>The command the <c>Run</c> value starts, or null when there is none.</summary>
    public static string? RegisteredCommand(string runKey = RunKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKey);
        return key?.GetValue(Name) as string;
    }

    /// <summary>Whether the Startup apps page turned the <c>Run</c> value off.</summary>
    public static bool TurnedOffInWindows(string approvedKey = ApprovedKey)
    {
        // The page writes 12 bytes: an odd first byte is off, with the time it was turned off after it.
        using var key = Registry.CurrentUser.OpenSubKey(approvedKey);
        return key?.GetValue(Name) is byte[] { Length: > 0 } state && (state[0] & 1) == 1;
    }

    /// <summary>
    /// The package's startup task, or null when the package has none or Windows does not hand it over. Only a
    /// packaged process calls this, and that call has no test: it would need an installed package.
    /// </summary>
    public static StartupTask? OpenTask()
    {
        try
        {
            // Sync because StartupService reads the task as it is made, and a UI binds its properties.
#pragma warning disable VSTHRD002 // Task.Run leaves the caller, so its context is not what it waits for.
            return Task.Run(static async () => await StartupTask.GetAsync(Name).AsTask().ConfigureAwait(false))
                .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>The program a sign-in command starts, past <c>conhost.exe --headless</c>, and the configuration it names.</summary>
    internal readonly record struct SignInLaunch(
        string? Program,
        string? Path,
        string? Name)
    {
        /// <summary>Whether the command names a configuration with <c>-p</c> or <c>-c</c>.</summary>
        public bool NamesConfiguration => Path is not null || Name is not null;
    }

    /// <summary>The file a path names in the end, past symbolic links, or the path itself when it is no link or cannot be read.</summary>
    internal static string Resolve(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, true)?.FullName ?? path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return path;
        }
    }

    /// <summary>
    /// Whether <paramref name="program"/> and <paramref name="thisExe"/> are one install, once <paramref name="resolve"/>
    /// has followed links. <c>winshell.exe</c> and <c>winshellw.exe</c> in the same folder are one install.
    /// </summary>
    internal static bool SameInstall(string program, string thisExe, Func<string, string> resolve, Log? log = null)
    {
        // A Run value can name a program that is not a path. That is not this install, and it must not throw.
        if (Full(program) is not { } programPath || Full(thisExe) is not { } herePath)
            return false;
        string started;
        string here;
        try
        {
            started = resolve(programPath);
            here = resolve(herePath);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException
                                      or UnauthorizedAccessException)
        {
            (log ?? Log.Default).Error("startup", e.ToString());
            return false;
        }

        if (string.Equals(started, here, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!WinshellHost.Is(started) || !WinshellHost.Is(here))
            return false;
        if (Full(started) is not { } startedFull || Full(here) is not { } hereFull)
            return false;
        return string.Equals(Path.GetDirectoryName(startedFull), Path.GetDirectoryName(hereFull),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The <c>winshell.exe</c> a sign-in entry is compared with. A configuration program is not that file: the host
    /// that started it is. With no such host, the process path stands.
    /// </summary>
    internal static string InstallExe(string processPath, string? supervisor)
    {
        return ConfigurationProject.FolderOf(processPath) is not null && supervisor is not null
            ? supervisor
            : processPath;
    }

    /// <summary>
    /// The project file <paramref name="launch"/> starts: the one <c>winshell run</c> would choose with the same
    /// <c>-p</c> or <c>-c</c>, or with neither, in <paramref name="configRoot"/>. Null when it would start none. A folder
    /// can hold several projects, so its path does not name one.
    /// </summary>
    internal static string? ProjectOf(SignInLaunch launch, string configRoot, Log? log = null)
    {
        // The CLI refuses a -c that is not a folder name, so such an entry starts nothing.
        if (launch.Name is { } name && !CommandWords.IsConfigName(name))
            return null;
        try
        {
            return ConfigurationProject.Find(launch.Path, launch.Name, configRoot, out _)?.ProjectFile;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            (log ?? Log.Default).Error("startup", e.ToString());
            return null;
        }
    }

    /// <summary>Whether <paramref name="launch"/> starts the project <paramref name="projectFile"/>.</summary>
    internal static bool Starts(SignInLaunch launch, string projectFile, string configRoot, Log? log = null)
    {
        return SamePath(ProjectOf(launch, configRoot, log), projectFile);
    }

    private static bool SamePath(string? left, string? right)
    {
        var a = Full(left);
        var b = Full(right);
        return a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Full(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException
                                      or IOException)
        {
            return null;
        }
    }
}
