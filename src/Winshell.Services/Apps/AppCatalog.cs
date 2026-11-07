using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Winshell.Native;

namespace Winshell.Apps;

/// <summary>The apps and the usage from one read.</summary>
internal sealed class AppSnapshot
{
    public static readonly AppSnapshot Empty = new([],
        new Dictionary<string, AppUsage>(StringComparer.OrdinalIgnoreCase));

    public AppSnapshot(IReadOnlyList<AppRecord> entries, IReadOnlyDictionary<string, AppUsage> usage)
    {
        Entries = entries;
        Usage = usage;
    }

    public IReadOnlyList<AppRecord> Entries { get; }

    public IReadOnlyDictionary<string, AppUsage> Usage { get; }
}

/// <summary>
/// The Start menu of some Programs folders and the installed packages, and how its apps open. Each read reads each
/// folder, the packages and the usage; one that fails keeps what the last good one found, and is warned once until it
/// reads again.
/// </summary>
/// <param name="log">Where failed reads are reported.</param>
/// <param name="folders">The Programs folders, the common one first: <see cref="ProgramsFolders"/>.</param>
/// <param name="packaged">Reads the packaged apps: <see cref="PackagedApps.Read"/>.</param>
/// <param name="usage">Reads the usage: <see cref="AppUsageReader.Read"/>.</param>
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

    private readonly object _gate = new();
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

    /// <summary>Raised on any thread when a Programs folder or the installed packages changed.</summary>
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

    /// <summary>Reads on its own thread: PackageManager and the shortcut files can block.</summary>
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
        // FileSystemWatcher delivers on a pool worker. A busy pool can hold a change, and the read behind it, forever.
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

    /// <summary>
    /// A watch ends when its folder goes away or its buffer overflows, and reports nothing after that. A read arms it
    /// again first, so that read sees whatever the old watch missed. A folder still missing waits for a later read.
    /// </summary>
    private void RearmLost()
    {
        string[] folders;
        lock (_gate)
        {
            folders = [.. _lost];
        }

        foreach (var folder in folders.Where(Directory.Exists))
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
        // The user's folder comes after the common one, so Combine keeps its shortcut of an id both have.
        var shortcuts = _shortcuts.SelectMany(folder => folder.Read()).ToList();
        return new AppSnapshot(AppIndex.Combine(shortcuts, _packaged.Read()), _usage.Read());
    }

    public string? OpenShortcut(string path, string startIn)
    {
        try
        {
            var start = new ProcessStartInfo(path) { UseShellExecute = true };
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

    /// <summary>Activates on its own STA thread, which <see cref="AppActivation"/> requires, and returns at once.</summary>
    public Task<string?> ActivateAsync(string appUserModelId, string arguments)
    {
        // Continuations run on this thread, so the launch posts its outcome without waiting for a pool worker.
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
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    public string? OpenAppsFolder(string appUserModelId)
    {
        return AppIdentity.OpenAppsFolder(appUserModelId);
    }

    /// <summary>One source of a read, and what its last good read found.</summary>
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

/// <summary>Packaged and unpackaged apps Windows shows in Start, from PackageManager.</summary>
internal static class PackagedApps
{
    /// <summary>Every app of the current user's packages. A package that fails to answer is left out and logged.</summary>
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
                // A package being installed or removed can fail to answer; the next read reads it again.
                log.Warn("apps", $"Skipped a package that did not answer: {e.Message}");
            }

        return result;
    }
}
