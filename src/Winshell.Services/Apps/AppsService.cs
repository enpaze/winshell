using System.Collections.ObjectModel;

namespace Winshell.Apps;

public sealed class AppsService : ObservableObject, IShellService
{
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
        _backend = backend ?? new AppsBackend(log, AppsBackend.ProgramsFolders(), () => PackagedApps.Read(log),
            () => AppUsageReader.Read(loop.Time.GetUtcNow()));
        _reader = new StateReader<AppSnapshot>(_ => _backend.ReadAsync(), Apply, loop, new FailureWarning(log, "apps"));
        _backend.Changed += OnChanged;
        _backend.Start();
        _reader.Request();
    }

    public ObservableList<AppEntry> Apps { get => Read(field); } = [];

    public ObservableList<AppEntry> Recent { get => Read(field); } = [];

    public ObservableList<AppEntry> Frequent { get => Read(field); } = [];

    private IReadOnlyList<AppRecord> Entries => Read(_entries);

    public AppEntry? ById(string id)
    {
        return Read(_byId, nameof(Entries)).GetValueOrDefault(id);
    }

    public AppEntry? Find(string name)
    {
        return AppIndex.Find(Entries, name) is { } found ? _byId[found.Id] : null;
    }

    public IReadOnlyList<AppEntry> Search(string query)
    {
        return AppIndex.Search(Entries, query, UsageFor, _loop.Time.GetUtcNow().ToUnixTimeMilliseconds())
            .Select(entry => _byId[entry.Id]).ToList();
    }

    public static int Score(string query, string name)
    {
        return AppRank.Name(query, name);
    }

    public static string? ProgramDescription(string path)
    {
        return AppIdentity.ProgramDescription(path);
    }

    public static string FallbackName(string appId)
    {
        return AppIdentity.FallbackName(appId);
    }

    public static string DisplayName(string appId)
    {
        return AppIdentity.DisplayName(appId);
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

    void IShellService.Stop()
    {
        if (_disposed)
            return;
        _disposed = true;
        _quiet?.Dispose();
        _backend.Changed -= OnChanged;
        _reader.Stop(_backend.Dispose);
    }
}

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

    public string Id { get => Read(field); }

    public string Name => Read(_entry).Name;

    public string Description => Read(_entry).Description;

    public string Icon => IconOf(Read(_entry));

    public string Target => Read(_entry).Target;

    public string Arguments => Read(_entry).Arguments;

    public string CommandLine => Read(_entry).CommandLine;

    public string StartIn => Read(_entry).StartIn;

    public string AppId => Read(_entry).AppId;

    public IReadOnlyList<string> Folders => Read(_entry).Folders;

    public bool Hidden => Read(_entry).Hidden;

    public bool Packaged => Read(_entry).Packaged;

    public int RunCount => Read(_usage).RunCount;

    public long LastUsed => Read(_usage).LastUsedUnixMilliseconds;

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

    public override string ToString()
    {
        return $"AppEntry({Id})";
    }

    private static string IconOf(AppRecord entry)
    {
        return AppIdentity.IsPackaged(entry.AppId) || (entry.AppId.Length > 0 && entry.Target.Length > 0)
            ? AppIdentity.IconPath(entry.AppId, entry.Target)
            : entry.Path;
    }
}
