using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.AccessControl;
using System.Security.Principal;
using Winshell.Apps;
using Winshell.Native;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class AppIndexTests
{
    private static readonly long Now = DateTimeOffset.Parse("2026-06-15T12:00:00Z", CultureInfo.InvariantCulture)
        .ToUnixTimeMilliseconds();

    [TestMethod]
    public async Task Shortcuts_keep_the_user_folder_and_hide_what_start_hides()
    {
        var root = Temp();
        try
        {
            var common = Path.Combine(root, "common");
            var user = Path.Combine(root, "user");
            var program = Path.Combine(root, "Program Files");
            Directory.CreateDirectory(program);
            var demo = Path.Combine(program, "demo.exe");
            File.WriteAllBytes(demo, []);
            var readme = Path.Combine(root, "readme.txt");
            await File.WriteAllTextAsync(readme, "notes", CurrentTest.Cancellation);
            var cmd = Cmd();

            WriteLink(Path.Combine(common, "Accessories", "Edit.lnk"), demo, "/common", program, "common editor");
            WriteLink(Path.Combine(user, "Accessories", "Edit.lnk"), demo, "/user", program, "user editor");
            WriteLink(Path.Combine(common, "Startup", "Boot.lnk"), cmd, "", "", "boot");
            WriteLink(Path.Combine(common, "Uninstall Edit.lnk"), demo, "", program, "remove");
            WriteLink(Path.Combine(common, "Readme.lnk"), readme, "", "", "notes");
            var hidden = Path.Combine(common, "Secret.lnk");
            WriteLink(hidden, cmd, "", "", "secret");
            File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);

            using var catalog = Catalog(common, user);
            var apps = (await catalog.ReadAsync()).Entries;

            var edit = Assert.ContainsSingle(app => app.Id == "Accessories/Edit", apps);
            Assert.AreEqual("/user", edit.Arguments);
            Assert.AreEqual("user editor", edit.Description);
            Assert.AreSequenceEqual(["Accessories"], edit.Folders);
            Assert.AreEqual(demo, edit.Target);
            Assert.AreEqual(program, edit.StartIn);
            Assert.AreEqual("demo", edit.AppId);
            Assert.AreEqual($"\"{demo}\" /user", edit.CommandLine);
            Assert.AreEqual(Path.Combine(user, "Accessories", "Edit.lnk"), edit.Path);
            Assert.IsFalse(edit.Hidden);
            Assert.IsFalse(edit.Packaged);

            Assert.DoesNotContain(app => app.Id.Contains("Startup", StringComparison.OrdinalIgnoreCase), apps);
            Assert.DoesNotContain(app => app.Name == "Secret", apps);

            var uninstall = Assert.ContainsSingle(app => app.Name == "Uninstall Edit", apps);
            Assert.IsTrue(uninstall.Hidden);
            var document = Assert.ContainsSingle(app => app.Name == "Readme", apps);
            Assert.IsTrue(document.Hidden);
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public void Packaged_apps_replace_a_shortcut_with_the_same_app_id()
    {
        Assert.IsNull(AppIndex.Packaged("", "Calculator", ""));
        Assert.IsNull(AppIndex.Packaged("id", "ms-resource:Name", ""));
        Assert.IsNull(AppIndex.Packaged("id", "  ", ""));

        var packaged = AppIndex.Packaged("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", " Calculator ",
            " A calculator ");
        Assert.IsNotNull(packaged);
        Assert.IsTrue(packaged.Packaged);
        Assert.AreEqual(packaged.Id, packaged.AppId);
        Assert.AreEqual(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", packaged.Path);
        Assert.AreEqual("Calculator", packaged.Name);
        Assert.AreEqual("A calculator", packaged.Description);
        Assert.IsFalse(packaged.Hidden);

        var shortcut = Entry("Calculator", "Calculator", packaged.AppId, target: Cmd());
        var custom = Entry("Editor", "Accessories/Editor", "demo", target: Cmd());
        var apps = AppIndex.Combine([shortcut, custom], [packaged]);

        Assert.DoesNotContain(app => app.Id == "Calculator", apps);
        var calc = Assert.ContainsSingle(app => app.Packaged, apps);
        Assert.AreEqual(packaged.Id, calc.Id);
        Assert.Contains(app => app.Id == "Accessories/Editor", apps);
        Assert.AreSequenceEqual([packaged.Id, "Accessories/Editor"], apps.Select(app => app.Id));
    }

    [TestMethod]
    public void Find_matches_id_then_app_id_then_name()
    {
        var hidden = Entry("Uninstall Notes", "Uninstall Notes", hidden: true);
        var notepad = Entry("Notepad", "Accessories/Notepad", "notepad");
        var apps = new List<AppRecord> { notepad, hidden };

        Assert.AreEqual("Accessories/Notepad", AppIndex.Find(apps, "Accessories/Notepad")!.Id);
        Assert.AreEqual("Accessories/Notepad", AppIndex.Find(apps, "accessories/notepad")!.Id);
        Assert.AreEqual("Accessories/Notepad", AppIndex.Find(apps, "notepad")!.Id);
        Assert.AreEqual("Accessories/Notepad", AppIndex.Find(apps, "Notepad")!.Id);
        Assert.AreEqual("Uninstall Notes", AppIndex.Find(apps, "Uninstall Notes")!.Id);
        Assert.IsNull(AppIndex.Find(apps, ""));
        Assert.IsNull(AppIndex.Find(apps, "missing"));
    }

    [TestMethod]
    public void Search_ranks_the_name_ahead_of_usage()
    {
        var notes = Entry("Notes");
        var notepad = Entry("Notepad");
        var oneNote = Entry("OneNote");
        var code = Entry("Visual Studio Code");
        var toolbox = Entry("Toolbox");
        var alpha = Entry("Alpha Tool");
        var paint = Entry("Paint", description: "draw pictures");
        var hidden = Entry("Uninstall Notes", hidden: true);
        var apps = new List<AppRecord> { oneNote, code, toolbox, alpha, paint, hidden, notepad, notes };

        Assert.AreSequenceEqual(["Notes", "Notepad", "OneNote"], Names("note", apps));
        Assert.AreSequenceEqual(["Notes", "Notepad", "OneNote"], Names("NOTE", apps));
        Assert.AreSequenceEqual(["Notepad"], Names("notpad", apps));
        Assert.AreSequenceEqual(["Visual Studio Code"], Names("vsc", apps));
        Assert.AreSequenceEqual(["Toolbox", "Alpha Tool"], Names("tool", apps));
        Assert.AreSequenceEqual(["Paint"], Names("draw", apps));
        Assert.IsEmpty(Names("xyz", apps));
        Assert.IsEmpty(Names("   ", apps));
        Assert.IsEmpty(Names("notes", [hidden]));

        var used = new Dictionary<string, AppUsage>(StringComparer.OrdinalIgnoreCase)
        {
            ["Notepad"] = new(50, Now - 1_000)
        };
        Assert.AreSequenceEqual(["Notepad", "Notes", "OneNote"],
            Names("note", apps, app => used.GetValueOrDefault(app.Id)));

        var heavy = new AppUsage(100, Now);
        Assert.IsTrue(AppRank.Score("note", notepad, default, Now) > AppRank.Score("draw", paint, heavy, Now));
        Assert.IsTrue(AppRank.Score("Notepad", notepad, default, Now) > AppRank.Score("note", notepad, default, Now));
        Assert.IsTrue(AppRank.Score("note", notepad, default, Now) > AppRank.Score("pad", notepad, default, Now));
        Assert.IsTrue(AppRank.Score("pad", notepad, default, Now) > AppRank.Score("notpad", notepad, default, Now));
        Assert.IsTrue(AppRank.Score("notpad", notepad, default, Now) > AppRank.Score("draw", paint, default, Now));
        Assert.AreEqual(0, AppRank.Score("xyz", notepad, heavy, Now));
    }

    [TestMethod]
    public void Recent_and_frequent_follow_usage()
    {
        var alpha = Entry("Alpha");
        var beta = Entry("Beta");
        var gamma = Entry("Gamma");
        var hidden = Entry("Hidden", hidden: true);
        var apps = new List<AppRecord> { alpha, beta, gamma, hidden };
        var usage = new Dictionary<string, AppUsage>(StringComparer.OrdinalIgnoreCase)
        {
            ["Alpha"] = new(2, 2_000),
            ["Beta"] = new(5, 1_000),
            ["Gamma"] = new(0, 0),
            ["Hidden"] = new(9, 3_000)
        };

        AppUsage Use(AppRecord app)
        {
            return usage.GetValueOrDefault(app.Id);
        }

        Assert.AreSequenceEqual(["Alpha", "Beta"], AppIndex.Recent(apps, Use).Select(app => app.Name).ToArray());
        Assert.AreSequenceEqual(["Beta", "Alpha"], AppIndex.Frequent(apps, Use).Select(app => app.Name).ToArray());
    }

    [TestMethod]
    public void Usage_reads_feature_counts_and_user_assist()
    {
        var when = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var now = when.AddDays(1);
        var data = Assist(14, when);
        var legacy = new byte[16];
        BitConverter.TryWriteBytes(legacy.AsSpan(0), 3);
        BitConverter.TryWriteBytes(legacy.AsSpan(8), when.ToFileTime());
        Assert.IsTrue(AppUsageReader.TryParse(data, now, out var parsed));
        Assert.AreEqual(14, parsed.RunCount);
        Assert.AreEqual(when.ToUnixTimeMilliseconds(), parsed.LastUsedUnixMilliseconds);
        Assert.IsTrue(AppUsageReader.TryParse(legacy, now, out var old));
        Assert.AreEqual(3, old.RunCount);
        Assert.IsFalse(AppUsageReader.TryParse(new byte[72], now, out _));

        var path = @"C:\Windows\System32\notepad.exe";
        var folder = AppIdentity.ExpandKnownFolder(@" {1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\notepad.exe".Trim());
        Assert.AreEqual(Path.Combine(Environment.SystemDirectory, "notepad.exe"), folder,
            StringComparer.OrdinalIgnoreCase);

        var usage = AppUsageReader.From(
            [new KeyValuePair<string, int>(path, 3)],
            [new KeyValuePair<string, int>(path, 9), new KeyValuePair<string, int>("OnlySwitched", 2)],
            [
                (@"P:\Jvaqbjf\Flfgrz32\abgrcnq.rkr", data),
                (@"{1NP14R77-02R7-4R5Q-O744-2RO1NR5198O7}\abgrcnq.rkr", data),
                ("HRZR_PGYPHNPbhag:pgbe", data)
            ], now);

        Assert.AreEqual(14, usage[path].RunCount);
        Assert.AreEqual(when.ToUnixTimeMilliseconds(), usage[path].LastUsedUnixMilliseconds);
        Assert.AreEqual(2, usage["OnlySwitched"].RunCount);
        Assert.AreEqual(14, usage[folder].RunCount);
        Assert.DoesNotContain(key => key.Contains("UEME_", StringComparison.Ordinal), usage.Keys);

        var notepad = Entry("Notepad", appId: "notepad", target: path);
        Assert.AreEqual(14, AppUsage.Lookup(usage, notepad).RunCount);
    }

    [TestMethod]
    public void Catalog_updates_when_a_shortcut_is_added_and_removed()
    {
        var root = Temp();
        var folder = Path.Combine(root, "programs");
        Directory.CreateDirectory(folder);
        var loop = new ManualEventLoop();
        var apps = new AppsService(loop, new Log(), Catalog(folder));
        using var stopApps = apps.Stopping();
        try
        {
            var link = Path.Combine(folder, "Probe.lnk");
            WriteLink(link, Cmd(), "", "", "probe");
            Until(loop, () => Listed(apps, "Probe"), "the new shortcut");
            File.Delete(link);
            Until(loop, () => !Listed(apps, "Probe"), "the removed shortcut to go");
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public void A_folder_that_comes_back_is_watched_again()
    {
        var root = Temp();
        var folder = Path.Combine(root, "programs");
        Directory.CreateDirectory(folder);
        WriteLink(Path.Combine(folder, "First.lnk"), Cmd(), "", "", "first");
        var loop = new ManualEventLoop();
        var apps = new AppsService(loop, new Log(), Catalog(folder));
        using var stopApps = apps.Stopping();
        try
        {
            Until(loop, () => Listed(apps, "First"), "the first read");
            Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            Until(loop, () => !Listed(apps, "First"), "the shortcut in the recreated folder to go");
            WriteLink(Path.Combine(folder, "Second.lnk"), Cmd(), "", "", "second");
            Until(loop, () => Listed(apps, "Second"), "the shortcut in the recreated folder");
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public void Disposing_does_not_wait_for_a_read_that_blocks()
    {
        using var reading = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var ended = false;
        var catalog = new AppsBackend(new Log(), [], () =>
        {
            reading.Set();
            release.Wait(TimeSpan.FromSeconds(10), CurrentTest.Cancellation);
            Volatile.Write(ref ended, true);
            return [];
        }, () => new Dictionary<string, AppUsage>(StringComparer.OrdinalIgnoreCase));
        var apps = new AppsService(new ManualEventLoop(), new Log(), catalog);
        try
        {
            reading.Wait(CurrentTest.Cancellation);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("The read did not start.");
        }

        apps.Stop();

        Assert.IsFalse(Volatile.Read(ref ended));
        release.Set();
        Wait.Until(() => Volatile.Read(ref ended), "the read to end");
    }

    [TestMethod]
    [DataRow(true, "apps: Could not read the installed apps: COMException: no packages")]
    [DataRow(false, "apps: Could not read how often apps are used: UnauthorizedAccessException: no usage")]
    public async Task A_read_that_keeps_failing_keeps_the_last_good_one_and_is_warned_once(bool packagesFail,
        string warning)
    {
        var root = Temp();
        var messages = new ConcurrentQueue<string>();
        var log = new Log { Sink = (_, category, message) => messages.Enqueue($"{category}: {message}") };
        var calc = AppIndex.Packaged("Contoso.Calc_1!App", "Calc", "")!;
        var reads = 0;
        try
        {
            using var catalog = new AppsBackend(log, [root], () =>
            {
                if (packagesFail && ++reads > 1)
                    throw new COMException("no packages");
                return [calc];
            }, () =>
            {
                if (!packagesFail && ++reads > 1)
                    throw new UnauthorizedAccessException("no usage");
                return new Dictionary<string, AppUsage>(StringComparer.OrdinalIgnoreCase) { [calc.Id] = new(3, 0) };
            });

            await catalog.ReadAsync();
            await catalog.ReadAsync();
            var snapshot = await catalog.ReadAsync();

            Assert.Contains(app => app.Id == calc.Id, snapshot.Entries);
            Assert.AreEqual(new AppUsage(3, 0), snapshot.Usage.GetValueOrDefault(calc.Id));
            Assert.ContainsSingle(message => message == warning, messages);
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task An_unreadable_folder_keeps_its_last_shortcuts_and_a_gone_one_has_none()
    {
        var root = Temp();
        var messages = new ConcurrentQueue<string>();
        var log = new Log { Sink = (_, category, message) => messages.Enqueue($"{category}: {message}") };
        var locked = Path.Combine(root, "locked");
        var open = Path.Combine(root, "open");
        WriteLink(Path.Combine(locked, "Kept.lnk"), Cmd(), "", "", "kept");
        Directory.CreateDirectory(open);
        try
        {
            using var catalog = new AppsBackend(log, [locked, open], () => [],
                () => new Dictionary<string, AppUsage>(StringComparer.OrdinalIgnoreCase));
            Assert.IsTrue(Listed(await catalog.ReadAsync(), "Kept"));

            var folder = new DirectoryInfo(locked);
            var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory,
                AccessControlType.Deny);
            var acl = folder.GetAccessControl();
            acl.AddAccessRule(deny);
            folder.SetAccessControl(acl);
            try
            {
                WriteLink(Path.Combine(open, "First.lnk"), Cmd(), "", "", "first");
                await catalog.ReadAsync();
                var snapshot = await catalog.ReadAsync();

                Assert.IsTrue(Listed(snapshot, "Kept"));
                Assert.IsTrue(Listed(snapshot, "First"));
                Assert.ContainsSingle(message => message.StartsWith("apps: Could not read the shortcuts",
                    StringComparison.Ordinal), messages);
            }
            finally
            {
                acl.RemoveAccessRule(deny);
                folder.SetAccessControl(acl);
            }

            Directory.Delete(locked, true);
            Assert.IsFalse(Listed(await catalog.ReadAsync(), "Kept"));
        }
        finally
        {
            Delete(root);
        }
    }

    private static bool Listed(AppSnapshot snapshot, string id)
    {
        return snapshot.Entries.Any(app => app.Id == id);
    }

    private static bool Listed(AppsService apps, string id)
    {
        return apps.ById(id) is not null;
    }

    private static void Until(ManualEventLoop loop, Func<bool> done, string what)
    {
        Wait.Until(done, what, () => loop.Advance(TimeSpan.FromSeconds(1)));
    }

    private static AppsBackend Catalog(params string[] folders)
    {
        return new AppsBackend(new Log(), folders, () => [],
            () => new Dictionary<string, AppUsage>(StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> Names(string query, IReadOnlyList<AppRecord> apps,
        Func<AppRecord, AppUsage>? usage = null)
    {
        return AppIndex.Search(apps, query, usage ?? (_ => default), Now).Select(app => app.Name).ToList();
    }

    private static AppRecord Entry(string name, string? id = null, string appId = "", string description = "",
        string target = "", string folders = "", bool hidden = false, bool packaged = false)
    {
        var identifier = id ?? name;
        return new AppRecord(identifier, name, description, target, target, "", "", appId, folders, hidden, packaged);
    }

    private static byte[] Assist(int count, DateTimeOffset when)
    {
        var data = new byte[72];
        BitConverter.TryWriteBytes(data.AsSpan(4), count);
        BitConverter.TryWriteBytes(data.AsSpan(60), when.ToFileTime());
        return data;
    }

    internal static void WriteLink(string path, string target, string arguments, string workingDirectory,
        string description)
    {
        object? link = null;
        try
        {
            link = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!)!;
            var shell = (IShellLinkW)link;
            var file = (IPersistFile)link;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            shell.SetPath(target);
            shell.SetArguments(arguments);
            shell.SetWorkingDirectory(workingDirectory);
            shell.SetDescription(description);
            file.Save(Path.GetFullPath(path), true);
        }
        finally
        {
            if (link is not null)
                Marshal.ReleaseComObject(link);
        }
    }

    private static string Cmd()
    {
        return Path.Combine(Environment.SystemDirectory, "cmd.exe");
    }

    internal static string Temp()
    {
        var path = TempFolder.NewPath();
        Directory.CreateDirectory(path);
        return path;
    }

    internal static void Delete(string path)
    {
        Wait.Until(() => Deleted(path), $"{path} to be deleted");
    }

    private static bool Deleted(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
