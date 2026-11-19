using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Winshell.Session;

internal static class SignInEntry
{
    public const string Name = "Winshell";

    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static string? RegisteredCommand(string runKey = RunKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKey);
        return key?.GetValue(Name) as string;
    }

    public static bool TurnedOffInWindows(string approvedKey = ApprovedKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey(approvedKey);
        return key?.GetValue(Name) is byte[] { Length: > 0 } state && (state[0] & 1) == 1;
    }

    public static StartupTask? OpenTask()
    {
        try
        {
#pragma warning disable VSTHRD002
            return Task.Run(static async () => await StartupTask.GetAsync(Name).AsTask().ConfigureAwait(false))
                .GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    internal static string? Command(string program, string? supervisor)
    {
        if (supervisor is null || !WinshellHost.Is(supervisor) ||
            ShellProject.RunPath(program) is not { } project)
            return null;
        return CommandLine.ForSignIn(supervisor, [CommandWords.Run, CommandWords.Path.Short!, project]);
    }

    internal readonly record struct SignInLaunch(
        string? Program,
        string? Path,
        string? Name)
    {
        public bool NamesShell => Path is not null || Name is not null;
    }

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

    internal static bool SameInstall(string program, string thisExe, Func<string, string> resolve, Log? log = null)
    {
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

    internal static string InstallExe(string processPath, string? supervisor)
    {
        return ShellProject.FolderOf(processPath) is not null && supervisor is not null
            ? supervisor
            : processPath;
    }

    internal static string? ProjectOf(SignInLaunch launch, string shellsRoot, Log? log = null)
    {
        if (launch.Name is { } name && !CommandWords.IsShellFolder(name))
            return null;
        try
        {
            return ShellProject.Find(launch.Path, launch.Name, shellsRoot, out _)?.ProjectFile;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            (log ?? Log.Default).Error("startup", e.ToString());
            return null;
        }
    }

    internal static bool Starts(SignInLaunch launch, string projectFile, string shellsRoot, Log? log = null)
    {
        return SamePath(ProjectOf(launch, shellsRoot, log), projectFile);
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
