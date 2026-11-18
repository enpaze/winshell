using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Winshell.Native;
using Winshell.Notifications;
using Winshell.Session;

namespace Winshell.Diagnostics;

public sealed record DoctorCheck(
    string Id,
    string Title,
    CheckStatus Status,
    string Message,
    string? Fix = null);

internal sealed record DoctorInstance(string Id, int Pid, string ConfigPath, string LogPath);

internal sealed record DoctorContext(ShellLocations Locations, string? Path, string? Config);

public static class Doctor
{
    internal const string BugReports = "https://github.com/enpaze/winshell/issues/new/choose";

    internal const int OldestReleaseBuild = 22621;

    internal static IReadOnlyList<DoctorCheck> Run(DoctorContext context)
    {
        return Report(context, ChooseUser(context.Locations.ConfigRoot, context.Path, context.Config));
    }

    private readonly record struct Chosen(
        ShellProject? Project,
        string? Problem,
        string Folder,
        string? SignInProject);

    private static Chosen ChooseUser(string configRoot, string? path, string? config)
    {
        var project = ShellProject.Find(path, config, configRoot, out var problem);
        var folder = project?.Folder ?? AskedFolder(configRoot, path, config);
        return new Chosen(project, problem, folder, project?.ProjectFile);
    }

    private static Chosen ChooseProcess(string configRoot, string program)
    {
        try
        {
            if (ShellProject.ForProgram(program) is { } own)
                return new Chosen(own, null, own.Folder, own.ProjectFile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        var project = ShellProject.Find(null, null, configRoot, out var problem);
        return new Chosen(project, problem, configRoot, null);
    }

    private static string AskedFolder(string configRoot, string? path, string? config)
    {
        if (path is not null)
            return Path.GetFullPath(path);
        if (config is not null)
            return Path.Combine(configRoot, config);
        return configRoot;
    }

    private static IReadOnlyList<DoctorCheck> Report(DoctorContext context, Chosen chosen)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var process = Environment.ProcessPath ?? "";
        var configFolder = chosen.Folder;
        var projectChecks = DoctorProject.Checks(chosen.Project, chosen.Problem).ToList();
        if (projectChecks[0].Status == CheckStatus.Pass && chosen.Project is { } project &&
            DoctorProbe.PreferencesProblem(context.Locations, project) is { } preferences)
            projectChecks[0] = WithPreferences(projectChecks[0], preferences);
        var instances = ListedInstances(context.Locations.Instances);
        var monitors = new Lazy<IReadOnlyList<DoctorScreens.MonitorFacts>>(DoctorProbe.Monitors);
        DoctorCheck[] checks =
        [
            Guard("windows", "Windows",
                () => Windows(DoctorProbe.WindowsBuild(), DoctorProbe.WindowsUpdateRevision(),
                    DoctorProbe.WindowsDisplayVersion(), DoctorProbe.WindowsServer(),
                    DoctorProbe.TransparencyEffectsOff(), ContrastTheme.IsOn())),
            Guard("dotnet", ".NET",
                () => DotNet(Environment.Version, RuntimeInformation.FrameworkDescription,
                    DoctorProbe.SelfContained(process))),
            .. projectChecks,
            Guard("folders", "Folders", () => Folders(
                [
                    ("the config folder", configFolder, DoctorProbe.CanWrite(configFolder)),
                    ("its local folder", context.Locations.LocalRoot, DoctorProbe.CanWrite(context.Locations.LocalRoot))
                ],
                instances.Where(i => DoctorProbe.IsElevated(i.Pid)).Select(i => i.Id).ToList(),
                Environment.IsPrivilegedProcess)),
            Guard("appBars", "App bars",
                () => DoctorScreens.AppBars(monitors.Value, DoctorProbe.EdgeWindows())),
            Guard("dpi", "DPI",
                () => DoctorScreens.Dpi(ThreadDpi.IsPerMonitorV2(), DoctorProbe.CompatibilityLayers(process),
                    monitors.Value)),
            Guard("signIn", "Sign-in", () =>
            {
                var project = chosen.SignInProject;
                return DoctorSignIn.Check(SignInProcess.Read(process, context.Locations.ConfigRoot, File.Exists,
                    SignInEntry.Resolve, project), context.Locations.ConfigRoot, project);
            }),
            Guard("notifications", "Notifications", () => Notifications(NotificationListenerAccess.Read().ToString())),
            Guard("instances", "Running", () => Instances(instances)),
            Guard("crashes", "Crashes",
                () => DoctorCrashes.CrashesIn(CrashReports.List(context.Locations.CrashRoot), now))
        ];
        return WithoutUserFolders(checks,
        [
            (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%")
        ]);
    }

    internal static IReadOnlyList<DoctorCheck> WithoutUserFolders(IReadOnlyList<DoctorCheck> checks,
        IReadOnlyList<(string Path, string Name)> folders)
    {
        var patterns = folders.Where(f => f.Path.Length > 0).OrderByDescending(f => f.Path.Length)
            .Select(f => (
                Pattern: new Regex(Regex.Escape(f.Path.TrimEnd('\\')) + @"(?![\w\-]|\.\w)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), f.Name))
            .ToList();

        string Replace(string text)
        {
            return patterns.Aggregate(text, (t, p) => p.Pattern.Replace(t, _ => p.Name));
        }

        return checks.Select(c => c with { Message = Replace(c.Message), Fix = c.Fix is null ? null : Replace(c.Fix) })
            .ToList();
    }

    internal static DoctorCheck Guard(string id, string title, Func<DoctorCheck> check)
    {
        try
        {
            return check();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return new DoctorCheck(id, title, CheckStatus.Warn, $"Could not check: {e.GetType().Name}: {e.Message}",
                $"Report it at {BugReports}.");
        }
    }

    internal static DoctorCheck Windows(int build, int revision, string? displayVersion, bool server = false,
        bool transparencyOff = false, bool contrastTheme = false)
    {
        var name = server ? "Windows Server" : build >= 22000 ? "Windows 11" : "Windows 10";
        var version = string.IsNullOrEmpty(displayVersion) ? "" : " " + displayVersion;
        var message = string.Create(CultureInfo.InvariantCulture, $"{name}{version}, build {build}.{revision}");
        if (transparencyOff)
            message += ". Transparency effects are off, so Mica and Acrylic look flat";
        if (contrastTheme)
            message += ". A contrast theme is on";
        if (transparencyOff || contrastTheme)
            message += ".";
        if (build < 22000)
            return new DoctorCheck("windows", "Windows", CheckStatus.Fail, message,
                "Winshell needs Windows 11. Upgrade this PC to Windows 11.");
        if (build < OldestReleaseBuild)
            return new DoctorCheck("windows", "Windows", CheckStatus.Warn, message,
                "Update Windows in Settings > Windows Update: Mica, Acrylic and other parts Winshell uses need Windows 11 version 22H2 or later.");
        return new DoctorCheck("windows", "Windows", CheckStatus.Pass, message);
    }

    internal static DoctorCheck DotNet(Version version, string description, bool selfContained)
    {
        var how = selfContained ? "built into Winshell" : "installed on this PC";
        if (version.Major < 11)
            return new DoctorCheck("dotnet", ".NET", CheckStatus.Fail, $"{description}, {how}",
                "Winshell needs .NET 11. Install the .NET 11 runtime, or use a Winshell release, which brings its own.");
        return new DoctorCheck("dotnet", ".NET", CheckStatus.Pass, $"{description}, {how}");
    }

    private static DoctorCheck WithPreferences(DoctorCheck check, string problem)
    {
        return check with
        {
            Status = CheckStatus.Warn,
            Message = check.Message.EndsWith('.') ? $"{check.Message} {problem}" : $"{check.Message}. {problem}",
            Fix = "Correct that file and save. Winshell keeps the last values it could read until the file parses."
        };
    }

    public static IReadOnlyList<DoctorCheck> ForThisProcess(string configRoot)
    {
        return Report(
            new DoctorContext(ShellLocations.Current with { ConfigRoot = configRoot }, null, null),
            ChooseProcess(configRoot, Environment.ProcessPath ?? ""));
    }

    private static List<DoctorInstance> ListedInstances(Instances instances)
    {
        try
        {
            return instances.List(false)
                .Select(i => new DoctorInstance(i.Info.Id, i.Info.Pid, i.Info.ConfigPath, i.Info.Log)).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    internal static DoctorCheck Folders(IReadOnlyList<(string Name, string Path, bool Writable)> folders,
        IReadOnlyList<string> elevatedInstances, bool asAdministrator = false)
    {
        var who = asAdministrator
            ? " This ran as administrator, so it says what an administrator can write. Run it without Run as administrator to check your account."
            : "";
        var locked = folders.Where(f => !f.Writable).ToList();
        if (locked.Count > 0)
        {
            var list = string.Join("; ", locked.Select(f => $"{f.Name}, {f.Path}"));
            return new DoctorCheck("folders", "Folders", CheckStatus.Fail, $"Winshell cannot write {list}.{who}",
                "Give your account write access to it in the folder's Properties > Security, or move it with WINSHELL_CONFIG_DIR.");
        }

        if (elevatedInstances.Count > 0)
            return new DoctorCheck("folders", "Folders", CheckStatus.Warn,
                $"Instance {string.Join(", ", elevatedInstances)} runs as administrator. Apps it starts run as administrator too, and other apps cannot drag onto it.{who}",
                "Close it and start Winshell without Run as administrator.");
        return new DoctorCheck("folders", "Folders", CheckStatus.Pass,
            $"Winshell can write {string.Join(" and ", folders.Select(f => f.Name))}{(who.Length > 0 ? "." + who : "")}");
    }

    internal static DoctorCheck Notifications(string access)
    {
        return access switch
        {
            "Allowed" => new DoctorCheck("notifications", "Notifications", CheckStatus.Pass,
                "Windows lets Winshell read notifications"),
            "Denied" => new DoctorCheck("notifications", "Notifications", CheckStatus.Warn,
                "Windows does not let Winshell read notifications, so a shell sees none.",
                "In Settings > Privacy & security > Notifications, let apps access your notifications."),
            "Unspecified" => new DoctorCheck("notifications", "Notifications", CheckStatus.Warn,
                "Winshell has not asked to read notifications yet, so a shell sees none.",
                "Have the shell call Shell.Notifications.RequestAccess from a button."),
            _ => new DoctorCheck("notifications", "Notifications", CheckStatus.Warn,
                "Windows does not let this Winshell read notifications: reading them needs an installed package with that permission. A shell sees none.",
                "Install Winshell from its package.")
        };
    }

    internal static DoctorCheck Instances(IReadOnlyList<DoctorInstance> instances)
    {
        if (instances.Count == 0)
            return new DoctorCheck("instances", "Running", CheckStatus.Pass, "No Winshell is running");
        var list = string.Join("; ",
            instances.Select(i => string.Create(CultureInfo.InvariantCulture,
                $"{i.Id} (process {i.Pid}) runs {i.ConfigPath}, log {i.LogPath}")));
        return new DoctorCheck("instances", "Running", CheckStatus.Pass, list);
    }

    public static string StatusName(CheckStatus status)
    {
        return status switch
        {
            CheckStatus.Pass => "pass",
            CheckStatus.Warn => "warn",
            _ => "fail"
        };
    }
}
