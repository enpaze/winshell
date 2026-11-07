using System.ComponentModel;
using Winshell.Apps;

namespace Winshell.Services.Tests;

/// <summary>
/// The Start menu's list over a fake backend, which supplies the reads and the launch outcomes, so nothing reads this
/// PC's Start menu and nothing is launched. Jump lists and recent documents read the test's own reader and folder.
/// </summary>
public sealed class AppsServiceTests : IDisposable
{
    private readonly string _folder =
        TempFolder.NewPath();

    private readonly ManualEventLoop _loop = new();
    private readonly FakeBackend _backend = new();
    private readonly List<string> _warnings = [];
    private readonly Log _log;

    public AppsServiceTests()
    {
        Directory.CreateDirectory(_folder);
        _log = new Log { Sink = (_, category, message) => _warnings.Add($"{category}: {message}") };
    }

    public void Dispose()
    {
        Directory.Delete(_folder, true);
    }

    private static AppRecord App(string name, bool hidden = false)
    {
        return AppIndex.Packaged($"{name}!App", name, "")! with { Hidden = hidden };
    }

    /// <summary>A shortcut as a scan of a Programs folder lists it.</summary>
    private static AppRecord Shortcut(string name)
    {
        return new AppRecord(
            $"Accessories/{name}", name, "", $@"C:\Menu\{name}.lnk", $@"C:\Tools\{name}.exe", "", @"C:\Users\Me",
            name.ToLowerInvariant(), "Accessories", false, false);
    }

    private static Dictionary<string, AppUsage> Usage(params (string Id, AppUsage Usage)[] rows)
    {
        return rows.ToDictionary(row => row.Id, row => row.Usage, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A service over a backend whose first read finds <paramref name="apps"/>, in name order as a read lists them.</summary>
    private AppsService Start(IReadOnlyList<AppRecord> apps, Dictionary<string, AppUsage>? usage = null)
    {
        _backend.Snapshot = new AppSnapshot(apps, usage ?? Usage());
        var service = new AppsService(_loop, _log, _backend);
        _loop.Process();
        return service;
    }

    /// <summary>A change the backend reports, read once it has been quiet for a second.</summary>
    private void Change(IReadOnlyList<AppRecord> apps, Dictionary<string, AppUsage>? usage = null)
    {
        _backend.Show(new AppSnapshot(apps, usage ?? Usage()));
        _loop.Advance(TimeSpan.FromSeconds(1));
    }

    private static List<string> Names(IEnumerable<AppEntry> entries)
    {
        return entries.Select(entry => entry.Name).ToList();
    }

    [Fact]
    public void Hidden_apps_stay_out_of_the_lists_and_can_still_be_found()
    {
        using var apps = Start([App("Alpha"), App("Uninstall", true), App("Zeta")]);

        Assert.Equal(["Alpha", "Zeta"], Names(apps.Apps));
        Assert.Equal("Uninstall", apps.ById("Uninstall!App")?.Name);
        Assert.Equal("Uninstall", apps.Find("uninstall")?.Name);
        Assert.Null(apps.ById("Nothing"));
        Assert.Null(apps.Find(""));
        Assert.Equal(["Alpha"], Names(apps.Search("alp")));
        Assert.Empty(apps.Search("uninstall"));
        Assert.Empty(apps.Search("   "));
    }

    [Fact]
    public void An_app_that_stays_is_the_same_object_and_an_app_that_goes_leaves_every_list()
    {
        var usage = Usage(("Beta!App", new AppUsage(3, 1000)));
        using var apps = Start([App("Alpha"), App("Beta")], usage);
        var alpha = apps.ById("Alpha!App")!;
        var beta = apps.ById("Beta!App")!;
        Assert.Equal(["Beta"], Names(apps.Recent));
        Assert.Equal(["Beta"], Names(apps.Frequent));

        Change([App("Alpha")], usage);

        Assert.Same(alpha, Assert.Single(apps.Apps));
        Assert.Empty(apps.Recent);
        Assert.Empty(apps.Frequent);
        Assert.Null(apps.ById("Beta!App"));

        // A launch from the entry that left is not counted when the app comes back.
        beta.Launch();
        _loop.Process();
        Change([App("Alpha"), App("Beta")], usage);
        Assert.Equal(3, apps.ById("Beta!App")!.RunCount);
    }

    [Fact]
    public void The_use_of_an_app_orders_recent_and_frequent()
    {
        using var apps = Start(
            [App("Alpha"), App("Beta")],
            Usage(("Alpha!App", new AppUsage(1, 2000)), ("Beta!App", new AppUsage(5, 1000))));

        Assert.Equal(["Alpha", "Beta"], Names(apps.Recent));
        Assert.Equal(["Beta", "Alpha"], Names(apps.Frequent));
        Assert.Equal(5, apps.ById("Beta!App")!.RunCount);
        Assert.Equal(2000, apps.ById("Alpha!App")!.LastUsed);
    }

    [Fact]
    public void A_shortcut_opens_with_its_start_in_folder_and_counts_as_a_use_at_once()
    {
        var notepad = Shortcut("Notepad");
        using var apps = Start([notepad]);
        var entry = apps.ById(notepad.Id)!;
        var changes = new List<string>();
        ((INotifyPropertyChanged)entry).PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        entry.Launch();

        Assert.Equal([@"shortcut C:\Menu\Notepad.lnk in C:\Users\Me"], _backend.Opened);
        Assert.Equal(1, entry.RunCount);
        Assert.Equal(_loop.Time.GetUtcNow().ToUnixTimeMilliseconds(), entry.LastUsed);
        Assert.Equal([nameof(AppEntry.RunCount), nameof(AppEntry.LastUsed)], changes);
        Assert.Same(entry, Assert.Single(apps.Recent));
        Assert.Same(entry, Assert.Single(apps.Frequent));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_packaged_launch_returns_before_activation_and_falls_back_on_the_loop()
    {
        using var apps = Start([App("Alpha")]);
        var entry = apps.ById("Alpha!App")!;
        // Like the real backend's, its continuations run on the thread that completes it.
        var activation = new TaskCompletionSource<string?>();
        _backend.Activation = activation.Task;
        var posts = 0;
        _loop.Woken += () => Interlocked.Increment(ref posts);

        // Activation is still running. A launch that waited for it would never finish, and nothing reaches the loop yet.
        await Task.Run(entry.Launch, TestContext.Current.CancellationToken)
            .WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["activate Alpha!App"], _backend.Opened);
        Assert.Equal(0, Volatile.Read(ref posts));

        // The outcome is posted to the loop, so nothing has happened yet.
        activation.SetResult("not registered");
        Assert.Equal(1, Volatile.Read(ref posts));
        Assert.Empty(_warnings);
        Assert.Equal(["activate Alpha!App"], _backend.Opened);
        Assert.Equal(0, entry.RunCount);

        _loop.Process();
        Assert.Equal(["apps: Failed to launch Alpha: not registered"], _warnings);
        Assert.Equal(["activate Alpha!App", "apps folder Alpha!App"], _backend.Opened);
        Assert.Equal(Environment.CurrentManagedThreadId, _backend.AppsFolderThread);
        Assert.Equal(1, entry.RunCount);
    }

    [Theory]
    [InlineData(false, new[] { "apps: Failed to launch Alpha: no such file" })]
    [InlineData(true,
        new[] { "apps: Failed to launch Alpha: not registered", "apps: Failed to launch Alpha: no such file" })]
    public void A_launch_that_opens_nothing_is_logged_and_not_counted(bool packaged, string[] warnings)
    {
        var alpha = packaged ? App("Alpha") : Shortcut("Alpha");
        using var apps = Start([alpha]);
        _backend.Activation = Task.FromResult<string?>("not registered");
        _backend.ShortcutError = "no such file";
        _backend.AppsFolderError = "no such file";

        apps.ById(alpha.Id)!.Launch();
        _loop.Process();

        Assert.Equal(warnings, _warnings);
        Assert.Equal(0, apps.ById(alpha.Id)!.RunCount);
        Assert.Empty(apps.Recent);
    }

    [Fact]
    public void Changes_are_read_once_they_have_been_quiet_for_a_second()
    {
        using var apps = Start([App("Alpha")]);

        _backend.Show(new AppSnapshot([App("Alpha"), App("Beta")], Usage()));
        _loop.Advance(TimeSpan.FromMilliseconds(900));
        _backend.Show(new AppSnapshot([App("Alpha"), App("Beta"), App("Gamma")], Usage()));
        _loop.Advance(TimeSpan.FromMilliseconds(900));
        Assert.Equal(["Alpha"], Names(apps.Apps));

        _loop.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(["Alpha", "Beta", "Gamma"], Names(apps.Apps));
    }

    [Fact]
    public void A_failed_read_is_warned_and_the_next_change_reads_again()
    {
        _backend.Failure = new InvalidCastException("read broke");
        using var apps = Start([App("Alpha")]);
        Assert.Equal(["apps: read broke"], _warnings);
        Assert.Empty(apps.Apps);

        _backend.Failure = null;
        Change([App("Alpha")]);

        Assert.Equal(["Alpha"], Names(apps.Apps));
    }

    [Fact]
    public void A_disposed_service_stops_following_the_backend()
    {
        var apps = Start([App("Alpha")]);

        apps.Dispose();
        Change([]);

        Assert.Single(apps.Apps);
    }

    [Fact]
    public void A_jump_list_is_read_for_an_id_and_stays_the_same_object()
    {
        var reader = new ScriptJumpLists();
        reader.Result["Contoso.Notes"] = JumpListRead.From(["Budget.xlsx"], ["Budget.xlsx", "Notes.txt"]);
        var lists = new JumpListService(reader);

        var found = lists.ForApp("Contoso.Notes");
        Assert.True(found.Available);
        Assert.Equal(["Budget.xlsx", "Notes.txt"], found.Destinations);

        var blank = lists.ForApp("  ");
        Assert.False(blank.Available);
        Assert.Empty(blank.Destinations);
        Assert.Equal(["Contoso.Notes"], reader.Asked);

        reader.Result["Contoso.Notes"] = JumpListRead.Missing;
        var missing = lists.ForApp("Contoso.Notes");
        Assert.Same(found, missing);
        Assert.False(missing.Available);
        Assert.Empty(missing.Destinations);
    }

    [Fact]
    public void Recent_documents_match_every_word_newest_first_and_a_blank_query_reads_nothing()
    {
        Touch("Budget report.lnk", 2);
        Touch("Report notes.lnk", 3);
        Touch("Budget.lnk", 1);
        var documents = new RecentDocuments(_folder);

        Assert.Equal(["Report notes", "Budget report"], documents.Search("report").Select(document => document.Name));
        var hit = Assert.Single(documents.Search("budget REPORT"));
        Assert.Equal(Path.Combine(_folder, "Budget report.lnk"), hit.Path);
        Assert.Empty(documents.Search("   "));
        Assert.Empty(documents.Search("budget missing"));
        Assert.Empty(new RecentDocuments(Path.Combine(_folder, "missing")).Search("report"));

        Touch("Agenda.txt", 4);
        var nested = Path.Combine(_folder, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "Secret Agenda.lnk"), []);
        Assert.Empty(documents.Search("agenda"));
        Assert.Empty(documents.Search(Path.GetFileName(_folder)));

        var same = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        File.WriteAllBytes(Path.Combine(_folder, "Beta.lnk"), []);
        File.WriteAllBytes(Path.Combine(_folder, "Able.lnk"), []);
        File.SetLastWriteTimeUtc(Path.Combine(_folder, "Beta.lnk"), same);
        File.SetLastWriteTimeUtc(Path.Combine(_folder, "Able.lnk"), same);
        Assert.Equal(["Able", "Beta"],
            documents.Search("a").Select(document => document.Name).Where(name => name is "Able" or "Beta"));

        Assert.Empty(documents.Search(""));
        Assert.Empty(documents.Search("\t"));
    }

    [Fact]
    public void Recent_documents_stop_at_the_limit()
    {
        for (var i = 0; i < RecentDocuments.Limit + 1; i++)
            Touch($"Doc {i:00}.lnk", i);

        var found = new RecentDocuments(_folder).Search("doc");

        Assert.Equal(RecentDocuments.Limit, found.Count);
        Assert.Equal("Doc 20", found[0].Name);
        Assert.Equal("Doc 01", found[^1].Name);
    }

    private void Touch(string name, int day)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, []);
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(day));
    }

    private sealed class ScriptJumpLists : IJumpListBackend
    {
        public List<string> Asked { get; } = [];

        public Dictionary<string, JumpListRead> Result { get; } = [];

        public JumpListRead Read(string appUserModelId)
        {
            Asked.Add(appUserModelId);
            return Result[appUserModelId];
        }
    }

    /// <summary>Supplies the reads and the launch outcomes. It opens nothing and records what it was asked to open.</summary>
    private sealed class FakeBackend : IAppsBackend
    {
        public event Action? Changed;

        /// <summary>What a read finds.</summary>
        public AppSnapshot Snapshot { get; set; } = AppSnapshot.Empty;

        /// <summary>What a read throws instead, when set.</summary>
        public Exception? Failure { get; set; }

        public Task<string?> Activation { get; set; } = Task.FromResult<string?>(null);

        public string? ShortcutError { get; set; }

        public string? AppsFolderError { get; set; }

        public List<string> Opened { get; } = [];

        public int? AppsFolderThread { get; private set; }

        public void Show(AppSnapshot snapshot)
        {
            Snapshot = snapshot;
            Changed?.Invoke();
        }

        public void Start()
        {
        }

        public Task<AppSnapshot> ReadAsync()
        {
            return Failure is null ? Task.FromResult(Snapshot) : Task.FromException<AppSnapshot>(Failure);
        }

        public string? OpenShortcut(string path, string startIn)
        {
            Opened.Add($"shortcut {path} in {startIn}");
            return ShortcutError;
        }

        public Task<string?> ActivateAsync(string appUserModelId, string arguments)
        {
            Opened.Add($"activate {appUserModelId}");
            return Activation;
        }

        public string? OpenAppsFolder(string appUserModelId)
        {
            Opened.Add($"apps folder {appUserModelId}");
            AppsFolderThread = Environment.CurrentManagedThreadId;
            return AppsFolderError;
        }

        public void Dispose()
        {
        }
    }
}
