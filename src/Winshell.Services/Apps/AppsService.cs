using System.Collections.ObjectModel;

namespace Winshell.Apps;

/// <summary>
/// The Start menu's shortcuts and packaged apps. <see cref="Find"/> finds the app of a window, and
/// <see cref="Search"/> ranks a typed query.
/// </summary>
public sealed class AppsService : ObservableObject, IDisposable
{
    // Installers write many files at once, so a read waits until they have been quiet this long.
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);

    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly IAppsBackend _backend;
    private readonly StateReader<AppSnapshot> _reader;
    private readonly Dictionary<string, AppEntry> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AppUsage> _bumps = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<AppRecord> _entries = [];

    private IReadOnlyDictionary<string, AppUsage> _usage =
        new Dictionary<string, AppUsage>(StringComparer.OrdinalIgnoreCase);

    private IDisposable? _quiet;
    private bool _disposed;

    internal AppsService(IEventLoop loop, Log log, IAppsBackend? backend = null)
    {
        _loop = loop;
        _log = log;
        _backend = backend ?? new AppCatalog(log, AppCatalog.ProgramsFolders(), () => PackagedApps.Read(log),
            () => AppUsageReader.Read(loop.Time.GetUtcNow()));
        _reader = new StateReader<AppSnapshot>(_ => _backend.ReadAsync(), Apply, loop, new MonitorWarning(log, "apps"));
        _backend.Changed += OnChanged;
        _backend.Start();
        _reader.Request();
    }

    /// <summary>The apps that are shown in Start, by name.</summary>
    public ObservableList<AppEntry> Apps { get => Read(field); } = [];

    /// <summary>Visible apps Windows has a last-used time for, newest first.</summary>
    public ObservableList<AppEntry> Recent { get => Read(field); } = [];

    /// <summary>Visible apps with a use count, most used first.</summary>
    public ObservableList<AppEntry> Frequent { get => Read(field); } = [];

    // What ById, Find and Search answer from. It changes with the lists.
    private IReadOnlyList<AppRecord> Entries => Read(_entries);

    /// <summary>An entry by its id: a shortcut's path under Programs, or a packaged app's AppUserModelID. Includes hidden entries.</summary>
    public AppEntry? ById(string id)
    {
        return Read(_byId, nameof(Entries)).GetValueOrDefault(id);
    }

    /// <summary>Finds an app by id, then by the app id a window carries, then by display name. Includes hidden entries.</summary>
    public AppEntry? Find(string name)
    {
        return AppIndex.Find(Entries, name) is { } found ? _byId[found.Id] : null;
    }

    /// <summary>Visible apps the query matches, best first. A blank query matches nothing.</summary>
    public IReadOnlyList<AppEntry> Search(string query)
    {
        return AppIndex.Search(Entries, query, UsageFor, _loop.Time.GetUtcNow().ToUnixTimeMilliseconds())
            .Select(entry => _byId[entry.Id]).ToList();
    }

    private void OnChanged()
    {
        _loop.Post(() =>
        {
            if (_disposed)
                return;
            _quiet?.Dispose();
            _quiet = _loop.Schedule(Debounce, _reader.Request);
        });
    }

    private void Apply(AppSnapshot snapshot)
    {
        _entries = snapshot.Entries;
        _usage = snapshot.Usage;
        var ids = _entries.Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, gone) in _byId.Where(pair => !ids.Contains(pair.Key)).ToArray())
        {
            _byId.Remove(id);
            _bumps.Remove(id);
            gone.Launched = null;
        }

        foreach (var entry in _entries)
            if (_byId.TryGetValue(entry.Id, out var kept))
                kept.Update(entry, UsageFor(entry));
            else
                _byId[entry.Id] = new AppEntry(entry, UsageFor(entry), _loop, _log, _backend) { Launched = OnLaunched };
        Publish();
    }

    private void OnLaunched(AppRecord entry)
    {
        if (_disposed)
            return;
        var shown = UsageFor(entry);
        _bumps[entry.Id] = new AppUsage(shown.RunCount + 1, _loop.Time.GetUtcNow().ToUnixTimeMilliseconds());
        if (_byId.TryGetValue(entry.Id, out var obj))
            obj.Update(entry, UsageFor(entry));
        Publish();
    }

    private void Publish()
    {
        Show(Apps, _entries.Where(entry => !entry.Hidden));
        Show(Recent, AppIndex.Recent(_entries, UsageFor));
        Show(Frequent, AppIndex.Frequent(_entries, UsageFor));
        Changed(nameof(Entries));
    }

    private void Show(ObservableList<AppEntry> list, IEnumerable<AppRecord> records)
    {
        list.Sync(records.Select(entry => _byId[entry.Id]), entry => entry, entry => entry, entry => entry,
            static (_, _) => { });
    }

    private AppUsage UsageFor(AppRecord entry)
    {
        var usage = AppUsage.Lookup(_usage, entry);
        if (_bumps.TryGetValue(entry.Id, out var bump))
            usage = AppUsage.Merge(usage, bump);
        return usage;
    }

    /// <summary>Stops following changes to the Start menu and the installed apps.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _quiet?.Dispose();
        _backend.Changed -= OnChanged;
        _reader.Stop(_backend.Dispose);
    }
}

/// <summary>One app of the Start menu.</summary>
public sealed class AppEntry : ObservableObject
{
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly IAppsBackend _backend;
    private AppRecord _entry;
    private AppUsage _usage;

    internal AppEntry(AppRecord entry, AppUsage usage, IEventLoop loop, Log log, IAppsBackend backend)
    {
        Id = entry.Id;
        _entry = entry;
        _usage = usage;
        _loop = loop;
        _log = log;
        _backend = backend;
    }

    internal Action<AppRecord>? Launched { get; set; }

    /// <summary>A shortcut's path under Programs, such as <c>Accessories/Notepad</c>, or a packaged app's AppUserModelID.</summary>
    public string Id { get => Read(field); }

    /// <summary>
    /// The name Start shows: the shortcut's file name without <c>.lnk</c>, or the packaged app's display name.
    /// </summary>
    public string Name => Read(_entry).Name;

    /// <summary>The shortcut's comment, or the packaged app's description. May be empty.</summary>
    public string Description => Read(_entry).Description;

    /// <summary>
    /// A path an icon is read from: <c>shell:AppsFolder\&lt;id&gt;</c> for a packaged app, or the program's file.
    /// A shortcut with no program keeps its own file.
    /// </summary>
    public string Icon => IconOf(Read(_entry));

    /// <summary>The program the shortcut opens, or <c>shell:AppsFolder\&lt;id&gt;</c> for a packaged app.</summary>
    public string Target => Read(_entry).Target;

    /// <summary>The shortcut's arguments. Empty for a packaged app.</summary>
    public string Arguments => Read(_entry).Arguments;

    /// <summary>A command line that describes how the app is opened.</summary>
    public string CommandLine => Read(_entry).CommandLine;

    /// <summary>The shortcut's Start in folder.</summary>
    public string StartIn => Read(_entry).StartIn;

    /// <summary>The AppUserModelID the app's windows carry, or its program's name in lower case.</summary>
    public string AppId => Read(_entry).AppId;

    /// <summary>The Start menu folders the shortcut is in. Empty for a packaged app.</summary>
    public IReadOnlyList<string> Folders => Read(_entry).Folders;

    /// <summary>True for an uninstaller or a document shortcut. Hidden apps stay out of <c>Apps</c>, <c>Search</c>, <c>Recent</c> and <c>Frequent</c>.</summary>
    public bool Hidden => Read(_entry).Hidden;

    /// <summary>True when the app came from PackageManager.</summary>
    public bool Packaged => Read(_entry).Packaged;

    /// <summary>How many times Windows, or <see cref="Launch"/>, has run the app.</summary>
    public int RunCount => Read(_usage).RunCount;

    /// <summary>When the app was last used, in Unix milliseconds, or 0 when Windows has no record.</summary>
    public long LastUsed => Read(_usage).LastUsedUnixMilliseconds;

    /// <summary>
    /// Opens the app as the Start menu does, through its shortcut or its AppUserModelID. A packaged app starts in
    /// the background, so this returns before it is up.
    /// </summary>
    public void Launch()
    {
        var entry = _entry;

        void Warn(string message)
        {
            _log.Warn("apps", $"Failed to launch {entry.Name}: {message}");
        }

        if (!entry.Packaged)
        {
            if (_backend.OpenShortcut(entry.Path, entry.StartIn) is { } error)
                Warn(error);
            else
                Launched?.Invoke(entry);
            return;
        }

        // Activation returns once the app is up, so its outcome, and the Apps folder when it failed, run on the loop.
        _ = _backend.ActivateAsync(entry.Id, entry.Arguments).ContinueWith(activated =>
        {
            var error = activated.Result;
            _loop.Post(() =>
            {
                if (error is not null)
                {
                    Warn(error);
                    if (_backend.OpenAppsFolder(entry.Id) is { } failed)
                    {
                        Warn(failed);
                        return;
                    }
                }

                Launched?.Invoke(entry);
            });
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    internal void Update(AppRecord entry, AppUsage usage)
    {
        var old = _entry;
        var oldUsage = _usage;
        _entry = entry;
        _usage = usage;
        Changed(old.Name != entry.Name, nameof(Name));
        Changed(old.Description != entry.Description, nameof(Description));
        Changed(IconOf(old) != IconOf(entry), nameof(Icon));
        Changed(old.Target != entry.Target, nameof(Target));
        Changed(old.Arguments != entry.Arguments, nameof(Arguments));
        Changed(old.CommandLine != entry.CommandLine, nameof(CommandLine));
        Changed(old.StartIn != entry.StartIn, nameof(StartIn));
        Changed(old.AppId != entry.AppId, nameof(AppId));
        Changed(old.Folder != entry.Folder, nameof(Folders));
        Changed(old.Hidden != entry.Hidden, nameof(Hidden));
        Changed(old.Packaged != entry.Packaged, nameof(Packaged));
        Changed(oldUsage.RunCount != usage.RunCount, nameof(RunCount));
        Changed(oldUsage.LastUsedUnixMilliseconds != usage.LastUsedUnixMilliseconds, nameof(LastUsed));
    }

    /// <summary>The entry's id, as <c>AppEntry(&lt;id&gt;)</c>, for logs.</summary>
    public override string ToString()
    {
        return $"AppEntry({Id})";
    }

    private static string IconOf(AppRecord entry)
    {
        return entry.AppId.Length > 0 && (entry.Packaged || entry.Target.Length > 0)
            ? AppIdentity.IconPath(entry.AppId, entry.Target)
            : entry.Path;
    }
}
