using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Winshell.Native;

namespace Winshell.Apps;

internal sealed class AppSnapshot(
    IReadOnlyList<AppRecord> entries,
    IReadOnlyDictionary<string, AppUsage> usage)
{
    public static readonly AppSnapshot Empty = new([],
        new Dictionary<string, AppUsage>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyList<AppRecord> Entries { get; } = entries;

    public IReadOnlyDictionary<string, AppUsage> Usage { get; } = usage;
}

internal sealed class AppCatalog(
    Log log,
    IReadOnlyList<string> folders,
    Func<IReadOnlyList<AppRecord>> packaged,
    Func<IReadOnlyDictionary<string, AppUsage>> usage) : IAppsBackend
{
    public static IReadOnlyList<string> ProgramsFolders()
    {
        return
        [
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs)
        ];
    }

    private readonly Lock _gate = new();
    private readonly Dictionary<string, DirectoryWatch> _watches = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lost = new(StringComparer.OrdinalIgnoreCase);
    private PackageCatalog? _packages;
    private bool _started, _disposed;

    private readonly Source<IReadOnlyList<AppRecord>>[] _shortcuts =
    [
        .. folders.Select(folder => new Source<IReadOnlyList<AppRecord>>(log, "Could not read the shortcuts",
            () => AppIndex.Shortcuts(folder), AppSnapshot.Empty.Entries))
    ];

    private readonly Source<IReadOnlyList<AppRecord>> _packaged =
        new(log, "Could not read the installed apps", packaged, AppSnapshot.Empty.Entries);

    private readonly Source<IReadOnlyDictionary<string, AppUsage>> _usage =
        new(log, "Could not read how often apps are used", usage, AppSnapshot.Empty.Usage);

    public event Action? Changed;

    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
                return;
            _started = true;
        }

        foreach (var folder in folders.Where(Directory.Exists))
            Arm(folder);
        try
        {
            _packages = PackageCatalog.OpenForCurrentUser();
            _packages.PackageInstalling += OnInstalling;
            _packages.PackageUninstalling += OnUninstalling;
            _packages.PackageUpdating += OnUpdating;
        }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException)
        {
            log.Warn("apps", $"Not following app installs: {e.Message}");
        }
    }

    public Task<AppSnapshot> ReadAsync()
    {
        return OwnThread.Run(Read);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        foreach (var watch in _watches.Values)
            watch.Dispose();
        _watches.Clear();
        if (_packages is not null)
        {
            _packages.PackageInstalling -= OnInstalling;
            _packages.PackageUninstalling -= OnUninstalling;
            _packages.PackageUpdating -= OnUpdating;
            _packages = null;
        }
    }

    private void Arm(string folder)
    {
        var watch = DirectoryWatch.Start(
            folder,
            DirectoryWatch.FileChanges | DirectoryWatch.NameChanges,
            _ => Changed?.Invoke(),
            () => Lost(folder),
            true);
        if (watch is null)
            return;
        DirectoryWatch? old;
        lock (_gate)
        {
            _watches.Remove(folder, out old);
            _watches[folder] = watch;
        }

        old?.Dispose();
    }

    private void Lost(string folder)
    {
        lock (_gate)
        {
            _lost.Add(folder);
        }

        Changed?.Invoke();
    }

    private void RearmLost()
    {
        string[] lost;
        lock (_gate)
        {
            lost = [.. _lost];
        }

        foreach (var folder in lost.Where(Directory.Exists))
        {
            lock (_gate)
            {
                _lost.Remove(folder);
            }

            Arm(folder);
        }
    }

    private void OnInstalling(PackageCatalog sender, PackageInstallingEventArgs args)
    {
        if (args.IsComplete)
            Changed?.Invoke();
    }

    private void OnUninstalling(PackageCatalog sender, PackageUninstallingEventArgs args)
    {
        if (args.IsComplete)
            Changed?.Invoke();
    }

    private void OnUpdating(PackageCatalog sender, PackageUpdatingEventArgs args)
    {
        if (args.IsComplete)
            Changed?.Invoke();
    }

    private AppSnapshot Read()
    {
        RearmLost();
        var shortcuts = _shortcuts.SelectMany(folder => folder.Read()).ToList();
        return new AppSnapshot(AppIndex.Combine(shortcuts, _packaged.Read()), _usage.Read());
    }

    public string? OpenShortcut(string path, string startIn)
    {
        try
        {
            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            var start = new ProcessStartInfo(explorer, $"\"{path}\"")
            { UseShellExecute = false, CreateNoWindow = true };
            if (startIn.Length > 0 && Directory.Exists(startIn))
                start.WorkingDirectory = startIn;
            using var _ = Process.Start(start);
            return null;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            return e.Message;
        }
    }

    public Task<string?> ActivateAsync(string appUserModelId, string arguments)
    {
        var done = new TaskCompletionSource<string?>();
        var thread = new Thread(() =>
        {
            try
            {
                AppActivation.Activate(appUserModelId, arguments);
                done.SetResult(null);
            }
            catch (Exception e)
            {
                done.SetResult(e.Message);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    public string? OpenAppsFolder(string appUserModelId)
    {
        return AppIdentity.OpenAppsFolder(appUserModelId);
    }

    private sealed class Source<T>(Log log, string what, Func<T> read, T empty)
    {
        private readonly MonitorWarning _warning = new(log, "apps", what);
        private T _found = empty;

        public T Read()
        {
            try
            {
                _found = read();
                _warning.Succeeded();
            }
            catch (Exception e) when (e is COMException or UnauthorizedAccessException or IOException
                                          or InvalidOperationException or SecurityException)
            {
                _warning.Failed(e);
            }

            return _found;
        }
    }
}

internal static class PackagedApps
{
    public static IReadOnlyList<AppRecord> Read(Log log)
    {
        var result = new List<AppRecord>();
        foreach (var package in new PackageManager().FindPackagesForUser("").ToList())
            try
            {
                if (package.IsFramework || package.IsResourcePackage)
                    continue;
                foreach (var app in package.GetAppListEntries())
                    if (AppIndex.Packaged(app.AppUserModelId, app.DisplayInfo.DisplayName,
                            app.DisplayInfo.Description) is { } entry)
                        result.Add(entry);
            }
            catch (Exception e) when (e is COMException or UnauthorizedAccessException or ArgumentException
                                          or FileNotFoundException or IOException or InvalidOperationException)
            {
                log.Warn("apps", $"Skipped a package that did not answer: {e.Message}");
            }

        return result;
    }
}
