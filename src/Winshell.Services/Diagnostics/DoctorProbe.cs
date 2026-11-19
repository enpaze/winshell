using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Microsoft.Win32;
using Winshell.Native;

namespace Winshell.Diagnostics;

internal static class DoctorProbe
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string LayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static int WindowsBuild()
    {
        return Environment.OSVersion.Version.Build;
    }

    public static int WindowsUpdateRevision()
    {
        return CurrentVersionValue("UBR") is int revision ? revision : 0;
    }

    public static bool WindowsServer()
    {
        return CurrentVersionValue("InstallationType") is string type
               && type.StartsWith("Server", StringComparison.OrdinalIgnoreCase);
    }

    public static string? WindowsDisplayVersion()
    {
        return CurrentVersionValue("DisplayVersion") as string;
    }

    public static bool TransparencyEffectsOff()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("EnableTransparency") is 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    private static object? CurrentVersionValue(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey);
        return key?.GetValue(name);
    }

    public static bool SelfContained(string exePath)
    {
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
        return string.IsNullOrEmpty(runtime)
               || string.Equals(Path.GetFullPath(runtime), Path.GetFullPath(Path.GetDirectoryName(exePath) ?? ""),
                   StringComparison.OrdinalIgnoreCase);
    }

    public static string PreferencesFile(ShellLocations locations, ShellProject project, RunProcess run,
        out string? unread)
    {
        var properties = new ShellProperties(project.ShellName(run, out unread));
        var name = ShellIdentity.Resolve(project.Program, locations, properties).ShellName;
        return Path.Combine(locations.ShellsRoot, "preferences", name + ".toml");
    }

    public static string? PreferencesProblem(string path)
    {
        var load = TomlDocument.Load(path);
        return load.Kind switch
        {
            TomlRead.Missing or TomlRead.Parsed => null,
            TomlRead.Unreadable => $"{path}: could not be read: {load.Message}",
            _ => load.Describe(path)
        };
    }

    public static bool CanWrite(string folder)
    {
        var dir = new DirectoryInfo(folder);
        while (dir is not null && !dir.Exists)
            dir = dir.Parent;
        if (dir is null)
            return false;
        try
        {
            using var probe = new FileStream(Path.Combine(dir.FullName, $".winshell-doctor-{Guid.NewGuid():N}.tmp"),
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool IsElevated(int pid)
    {
        return ProcessElevation.TryRead((uint)pid, out var tokenDenied) ??
               (tokenDenied && !Environment.IsPrivilegedProcess);
    }

    public static IReadOnlyList<DoctorScreens.MonitorFacts> Monitors()
    {
        return Screens.List()
            .Select(m => new DoctorScreens.MonitorFacts(m.Name, m.Bounds.left, m.Bounds.top, m.Bounds.right,
                m.Bounds.bottom, m.Work.left, m.Work.top, m.Work.right, m.Work.bottom, m.Scale))
            .ToList();
    }

    public static IReadOnlyList<DoctorScreens.WindowFacts> EdgeWindows()
    {
        var result = new List<DoctorScreens.WindowFacts>();
        foreach (var hwnd in TopLevelWindows.List())
        {
            if (!PInvoke.IsWindowVisible((HWND)hwnd) || PInvoke.IsIconic((HWND)hwnd) || TopLevelWindows.IsCloaked(hwnd) ||
                !PInvoke.GetWindowRect((HWND)hwnd, out var rect))
                continue;
            PInvoke.GetWindowThreadProcessId((HWND)hwnd, out var pid);
            result.Add(new DoctorScreens.WindowFacts(TopLevelWindows.ClassName(hwnd), rect.left, rect.top, rect.right,
                rect.bottom, ProgramOf((int)pid)));
        }

        return result;
    }

    private static string ProgramOf(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName + ".exe";
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return "";
        }
    }

    public static IReadOnlyList<string> CompatibilityLayers(string exePath)
    {
        var full = Path.GetFullPath(exePath);
        var layers = new List<string>();
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = hive.OpenSubKey(LayersKey);
            if (key is null)
                continue;
            foreach (var name in key.GetValueNames())
                if (string.Equals(name, full, StringComparison.OrdinalIgnoreCase) && key.GetValue(name) is string value)
                    layers.Add(value);
        }

        return layers;
    }
}
