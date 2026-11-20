using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Winshell.AppWindows;
using Winshell.Winland;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class WinlandServiceTests
{
    private const string Monitors = """
                                    [{"id":1,"name":"\\\\.\\DISPLAY2","description":"Left","x":-1920,"y":0,"width":1920,"height":1080,"scale":1.5,"focused":false,
                                      "activeWorkspace":{"id":3,"name":"3"}},
                                     {"id":0,"name":"\\\\.\\DISPLAY1","description":"Main","x":0,"y":0,"width":2560,"height":1440,"scale":1,"focused":true,
                                      "activeWorkspace":{"id":1,"name":"1"}}]
                                    """;

    private const string Workspaces = """
                                      [{"id":3,"name":"3","monitor":"\\\\.\\DISPLAY1","monitorId":0,"hasFullscreen":false},
                                       {"id":1,"name":"1","monitor":"\\\\.\\DISPLAY1","monitorId":0,"hasFullscreen":true},
                                       {"id":3,"name":"3","monitor":"\\\\.\\DISPLAY2","monitorId":1,"hasFullscreen":false},
                                       {"name":"notes","monitor":"\\\\.\\DISPLAY1","monitorId":0},
                                       {"name":"special:special"}]
                                      """;

    private const string Clients = """
                                   [{"address":"0x10","title":"Editor","workspace":{"id":1,"name":"1","monitor":"\\\\.\\DISPLAY1"}},
                                    {"address":"0x20","title":"Browser","workspace":{"id":3,"name":"3","monitor":"\\\\.\\DISPLAY2"}}]
                                   """;

    private readonly ManualEventLoop _loop = new();
    private readonly FakeBackend _backend = new();

    private WinlandService Connected()
    {
        _backend.Replies["j/get monitors"] = Monitors;
        _backend.Replies["j/get workspaces"] = Workspaces;
        _backend.Replies["j/get windows"] = Clients;
        _backend.Replies["j/get window"] = """{"address":"0x10"}""";
        _backend.Connected = true;
        var service = new WinlandService(_loop, Log.Default, _backend);
        _loop.Process();
        return service;
    }

    private const string Display1 = @"\\.\DISPLAY1";
    private const string Display2 = @"\\.\DISPLAY2";

    private void Raise(string line)
    {
        _backend.Raise(line);
        _loop.Process();
    }

    private static string Event(string name, object? fields = null)
    {
        var json = fields is null ? new JsonObject() : JsonSerializer.SerializeToNode(fields)!.AsObject();
        json.Insert(0, "event", name);
        return json.ToJsonString();
    }

    private static object Workspace(int id)
    {
        return new { id, name = id.ToString(CultureInfo.InvariantCulture) };
    }

    private static string Opened(string address, int workspace, string title)
    {
        return Event("openwindow",
            new { address, workspace = Workspace(workspace), monitor = Display1, @class = "App", title });
    }

    private static string Focus(string monitor, int workspace, string address)
    {
        return Event("focus", new
        {
            monitor,
            workspace = Workspace(workspace),
            window = new { address, title = "", @class = "App", exe = "app.exe" },
        });
    }

    private static List<string> Watch(INotifyPropertyChanged source)
    {
        var changes = new List<string>();
        source.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        return changes;
    }

    private static WindowRead Program(string title, string program)
    {
        return new WindowRead(title, false, false, false, "M1", "", "", program);
    }

    [TestMethod]
    public void A_connection_fills_the_lists_in_order()
    {
        var service = Connected();
        using var stopService = service.Stopping();

        Assert.AreSequenceEqual([0, 1], service.Monitors.Select(m => m.Id));
        Assert.AreSequenceEqual(["special:special", "notes", "1", "3", "3"], service.Workspaces.Select(w => w.Name));
        Assert.AreSequenceEqual(["10", "20"], service.Windows.Select(t => t.Address));
        Assert.AreEqual(0x10, service.Windows[0].Handle);
        Assert.AreEqual(-99, service.Workspaces.Single(w => w.Name == "special:special").Id);
        Assert.AreEqual(-1, service.Workspaces.Single(w => w.Name == "notes").Id);

        var main = service.FocusedMonitor!;
        Assert.AreEqual(@"\\.\DISPLAY1", main.Name);
        Assert.AreEqual("Main", main.Description);
        Assert.AreEqual(2560, main.Width);
        Assert.IsTrue(main.Focused);
        Assert.AreEqual("1", main.ActiveWorkspace!.Name);
        Assert.AreEqual("Main", main.LastIpcObject!.Value.GetProperty("description").GetString());

        var left = service.Monitors[1];
        Assert.AreEqual((-1920, 0, 1.5), (left.X, left.Y, left.Scale));
        Assert.IsFalse(left.Focused);

        var focused = service.FocusedWorkspace!;
        Assert.AreSame(main.ActiveWorkspace, focused);
        Assert.IsTrue(focused.Focused);
        Assert.IsTrue(focused.Active);
        Assert.IsTrue(focused.HasFullscreen);
        Assert.AreEqual("1", focused.Name);
        Assert.IsNull(typeof(WinlandWorkspace).GetProperty("Address"));

        Assert.AreSame(service.Windows[0], service.ActiveWindow);
        Assert.IsTrue(service.ActiveWindow!.Activated);
        Assert.AreEqual("Editor", service.ActiveWindow.Title);
        Assert.AreSame(focused, service.ActiveWindow.Workspace);
        Assert.AreSame(main, service.ActiveWindow.Monitor);
        Assert.AreSame(service.ActiveWindow, Assert.ContainsSingle(focused.Windows));
    }

    [TestMethod]
    public void Two_workspaces_with_one_number_stay_apart_by_monitor()
    {
        var service = Connected();
        using var stopService = service.Stopping();

        var threes = service.Workspaces.Where(w => w.Name == "3").ToArray();

        Assert.AreEqual(2, threes.Length);
        Assert.AreSequenceEqual([@"\\.\DISPLAY1", @"\\.\DISPLAY2"], threes.Select(w => w.Monitor!.Name));
        Assert.IsFalse(threes[0].Active);
        Assert.IsTrue(threes[1].Active);
        Assert.AreEqual("20", Assert.ContainsSingle(threes[1].Windows).Address);
        Assert.IsEmpty(threes[0].Windows);
    }

    [TestMethod]
    public void A_window_is_the_app_window_with_its_handle()
    {
        var windows = new FakeWindowBackend();
        windows.Show(0x10);
        var list = new AppWindowsService(_loop, Log.Default, backend: windows);
        using var stopList = list.Stopping();
        var service = Connected();
        using var stopService = service.Stopping();
        var editor = service.Windows.Single(window => window.Address == "10");
        var browser = service.Windows.Single(window => window.Address == "20");
        Assert.IsNull(editor.Window);

        var changes = Watch(editor);
        service.Attach(list);

        Assert.AreSame(list.Windows.Single(window => window.Hwnd == 0x10), editor.Window);
        Assert.AreSequenceEqual([nameof(WinlandWindow.Window)], changes);
        Assert.IsNull(browser.Window);

        windows.Show(0x20);
        _loop.Process();
        Assert.AreSame(list.Windows.Single(window => window.Hwnd == 0x20), browser.Window);

        windows.Destroy(0x10);
        _loop.Process();
        Assert.IsNull(editor.Window);
        Assert.Contains(nameof(WinlandWindow.Window), changes);
    }

    [TestMethod]
    public void A_window_list_attached_before_winland_reports_clients_still_resolves_them()
    {
        var windows = new FakeWindowBackend();
        windows.Show(0x10);
        windows.Show(0x20);
        var list = new AppWindowsService(_loop, Log.Default, backend: windows);
        using var stopList = list.Stopping();
        _backend.Replies["j/get monitors"] = Monitors;
        _backend.Replies["j/get workspaces"] = Workspaces;
        _backend.Replies["j/get windows"] = Clients;
        _backend.Replies["j/get window"] = """{"address":"0x10"}""";
        _backend.Connected = true;
        var service = new WinlandService(_loop, Log.Default, _backend);
        using var stopService = service.Stopping();
        service.Attach(list);
        _loop.Process();

        Assert.AreSame(list.Windows.Single(window => window.Hwnd == 0x10),
            service.Windows.Single(window => window.Address == "10").Window);
        Assert.AreSame(list.Windows.Single(window => window.Hwnd == 0x20),
            service.Windows.Single(window => window.Address == "20").Window);
    }

    [TestMethod]
    public void A_workspace_shows_each_app_icon_once_in_window_order()
    {
        var windows = new FakeWindowBackend();
        windows.Show(0x10, Program("Editor", @"C:\editor.exe"));
        windows.Show(0x30, Program("Browser", @"C:\browser.exe"));
        windows.Show(0x40, Program("Second editor", @"C:\editor.exe"));
        windows.Show(0x50, Program("Third editor", @"C:\EDITOR.EXE"));
        var list = new AppWindowsService(_loop, Log.Default, backend: windows);
        using var stopList = list.Stopping();
        var service = Connected();
        using var stopService = service.Stopping();
        service.Attach(list);
        var main = service.FocusedWorkspace!;

        Raise(Opened("0x30", 1, "Browser"));
        Raise(Opened("0x40", 1, "Second editor"));
        Raise(Opened("0x50", 1, "Third editor"));

        Assert.AreEqual(4, main.Windows.Count);
        Assert.AreSequenceEqual([@"C:\editor.exe", @"C:\browser.exe"], main.AppIcons);
    }

    [TestMethod]
    public void A_workspace_without_windows_shows_no_app_icons()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var three = service.Workspaces.First(w => w.Name == "3" && w.Monitor!.Name == @"\\.\DISPLAY1");

        Assert.IsEmpty(three.Windows);
        Assert.IsEmpty(three.AppIcons);
    }

    [TestMethod]
    public void A_winland_window_with_no_app_window_shows_no_icon()
    {
        var windows = new FakeWindowBackend();
        windows.Show(0x10, Program("Editor", @"C:\editor.exe"));
        var list = new AppWindowsService(_loop, Log.Default, backend: windows);
        using var stopList = list.Stopping();
        var service = Connected();
        using var stopService = service.Stopping();
        var main = service.FocusedWorkspace!;
        var left = service.Monitors[1].ActiveWorkspace!;
        Assert.IsEmpty(main.AppIcons);

        service.Attach(list);

        Assert.AreSequenceEqual([@"C:\editor.exe"], main.AppIcons);
        Assert.ContainsSingle(left.Windows);
        Assert.IsEmpty(left.AppIcons);
    }

    [TestMethod]
    public void An_app_icon_follows_its_window_open_move_and_close()
    {
        var windows = new FakeWindowBackend();
        windows.Show(0x10, Program("Editor", @"C:\editor.exe"));
        windows.Show(0x30, Program("Browser", @"C:\browser.exe"));
        var list = new AppWindowsService(_loop, Log.Default, backend: windows);
        using var stopList = list.Stopping();
        var service = Connected();
        using var stopService = service.Stopping();
        service.Attach(list);
        var main = service.FocusedWorkspace!;
        var three = service.Workspaces.First(w => w.Name == "3" && w.Monitor!.Name == @"\\.\DISPLAY1");

        Raise(Opened("0x30", 1, "Browser"));
        Assert.AreSequenceEqual([@"C:\editor.exe", @"C:\browser.exe"], main.AppIcons);

        Raise(Event("movewindow", new { address = "0x30", workspace = Workspace(3), monitor = Display1 }));
        Assert.AreSequenceEqual([@"C:\editor.exe"], main.AppIcons);
        Assert.AreSequenceEqual([@"C:\browser.exe"], three.AppIcons);

        Raise(Event("closewindow", new { address = "0x30" }));
        Assert.IsEmpty(three.AppIcons);
        Assert.AreSequenceEqual([@"C:\editor.exe"], main.AppIcons);
    }

    [TestMethod]
    public void An_event_updates_the_same_objects_then_is_raised()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var window = service.Windows[0];
        var changes = Watch(window);
        var seen = new List<(string Name, string Title)>();
        service.RawEvent += e => seen.Add((e.Name, window.Title));

        Raise(Event("windowtitle", new { address = "0x10", title = "New, title" }));

        Assert.AreEqual("New, title", window.Title);
        Assert.AreSequenceEqual([nameof(WinlandWindow.Title)], changes);
        Assert.AreEqual(("windowtitle", "New, title"), Assert.ContainsSingle(seen));
        Assert.AreSame(window, service.Windows[0]);
    }

    [TestMethod]
    public void A_raised_event_carries_its_json()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        WinlandEvent? seen = null;
        service.RawEvent += e => seen = e;

        Raise(Opened("0x30", 1, "Notes"));

        Assert.AreEqual("openwindow", seen!.Name);
        Assert.AreEqual("Notes", seen.Data.GetProperty("title").GetString());
    }

    [TestMethod]
    public void A_line_that_is_not_a_json_event_is_skipped()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var seen = new List<WinlandEvent>();
        service.RawEvent += seen.Add;

        Raise("windowtitle>>10,Old text event");
        Raise("""{"address":"0x10"}""");

        Assert.IsEmpty(seen);
        Assert.AreEqual("Editor", service.Windows[0].Title);
    }

    [TestMethod]
    public void Focus_on_a_monitor_shows_its_workspace_and_activates_its_window()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var left = service.Monitors[1];

        Raise(Focus(Display2, 3, "0x20"));

        Assert.AreSame(left, service.FocusedMonitor);
        Assert.AreEqual("3", left.ActiveWorkspace!.Name);
        Assert.AreSame(service.Windows.Single(w => w.Address == "20"), service.ActiveWindow);
    }

    [TestMethod]
    public void Focus_with_no_window_clears_the_active_window()
    {
        var service = Connected();
        using var stopService = service.Stopping();

        Raise(Event("focus", new { monitor = Display1, workspace = Workspace(1), window = (object?)null }));

        Assert.IsNull(service.ActiveWindow);
        Assert.AreEqual("1", service.FocusedWorkspace!.Name);
    }

    [TestMethod]
    public void Focus_on_a_special_workspace_keeps_the_monitor_s_workspace()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var main = service.FocusedMonitor!;
        var shown = main.ActiveWorkspace;

        Raise(Event("focus", new
        {
            monitor = Display1,
            workspace = new { id = -99, name = "special:special" },
            window = (object?)null,
        }));

        Assert.AreSame(shown, main.ActiveWorkspace);
    }

    [TestMethod]
    public void A_query_Winland_refuses_is_logged_once()
    {
        var lines = new List<string>();
        var log = new Log { Sink = (_, _, message) => lines.Add(message) };
        _backend.Replies["j/get workspaces"] = "error: unknown command 'workspaces'";
        _backend.Connected = true;
        var service = new WinlandService(_loop, log, _backend);
        using var stopService = service.Stopping();
        _loop.Process();

        service.RefreshWorkspaces();
        _loop.Process();

        Assert.ContainsSingle(line => line.Contains("j/get workspaces", StringComparison.Ordinal), lines);
        Assert.Contains("unknown command", lines.Single(line => line.Contains("j/get workspaces", StringComparison.Ordinal)),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void Showing_a_workspace_moves_active_and_focus()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var main = service.FocusedMonitor!;
        var old = service.FocusedWorkspace!;
        var three = service.Workspaces.First(w => w.Name == "3" && w.Monitor == main);

        Raise(Event("workspace", new { workspace = Workspace(3), monitor = Display1 }));

        Assert.AreSame(three, service.FocusedWorkspace);
        Assert.AreSame(three, main.ActiveWorkspace);
        Assert.IsTrue(three.Active);
        Assert.IsTrue(three.Focused);
        Assert.IsFalse(old.Active);
        Assert.IsFalse(old.Focused);
    }

    [TestMethod]
    public void Focus_moves_between_monitors()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var left = service.Monitors[1];

        Raise(Focus(Display2, 3, "0x20"));

        Assert.AreSame(left, service.FocusedMonitor);
        Assert.IsTrue(left.Focused);
        Assert.IsFalse(service.Monitors[0].Focused);
        Assert.AreSame(left.ActiveWorkspace, service.FocusedWorkspace);
    }

    [TestMethod]
    public void Windows_open_move_and_close()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var main = service.FocusedWorkspace!;
        var three = service.Workspaces.First(w => w.Name == "3" && w.Monitor!.Name == @"\\.\DISPLAY1");

        Raise(Opened("0x30", 1, "Title, with comma"));
        var window = service.Windows.Single(t => t.Address == "30");
        Assert.AreEqual("Title, with comma", window.Title);
        Assert.Contains(window, main.Windows);

        Raise(Event("movewindow", new { address = "0x30", workspace = Workspace(3), monitor = Display1 }));
        Assert.AreSame(three, window.Workspace);
        Assert.Contains(window, three.Windows);
        Assert.DoesNotContain(window, main.Windows);

        Raise(Focus(Display1, 3, "0x30"));
        Assert.AreSame(window, service.ActiveWindow);
        Assert.IsFalse(service.Windows[0].Activated);

        Raise(Event("closewindow", new { address = "0x30" }));
        Assert.DoesNotContain(window, service.Windows);
        Assert.IsEmpty(three.Windows);
        Assert.IsNull(service.ActiveWindow);
    }

    [TestMethod]
    public void A_window_arriving_reaches_its_workspace_and_the_workspace_list()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var workspace = service.FocusedWorkspace!;
        var workspaceChanges = new List<string?>();
        var listChanges = new List<string?>();
        workspace.PropertyChanged += (_, e) => workspaceChanges.Add(e.PropertyName);
        service.Workspaces.PropertyChanged += (_, e) => listChanges.Add(e.PropertyName);

        Raise(Opened("0x30", 1, "Notes"));

        Assert.Contains(nameof(WinlandWorkspace.Windows), workspaceChanges);
        Assert.Contains("Item[]", listChanges);
    }

    [TestMethod]
    public void An_urgent_window_marks_its_workspace_until_it_is_focused()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var other = service.Windows[1];
        var workspace = other.Workspace!;

        Raise(Event("urgent", new { address = "0x20" }));
        Assert.IsTrue(other.Urgent);
        Assert.IsTrue(workspace.Urgent);

        Raise(Focus(Display2, 3, "0x20"));
        Assert.IsFalse(other.Urgent);
        Assert.IsFalse(workspace.Urgent);
    }

    [TestMethod]
    public void A_workspace_comes_and_goes()
    {
        var service = Connected();
        using var stopService = service.Stopping();

        Raise(Event("createworkspace", new { workspace = Workspace(7), monitor = Display1 }));
        var seven = service.Workspaces.Single(w => w.Name == "7");
        Assert.AreEqual(7, seven.Id);

        Raise(Event("destroyworkspace", new { workspace = Workspace(7), monitor = Display1 }));
        Assert.DoesNotContain(seven, service.Workspaces);
    }

    [TestMethod]
    public void A_monitor_that_goes_leaves_its_workspaces_with_none()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var left = service.Monitors[1];
        var three = left.ActiveWorkspace!;

        Raise(Event("monitorremoved", new { id = 1, name = Display2, description = "Left" }));

        Assert.DoesNotContain(left, service.Monitors);
        Assert.IsNull(three.Monitor);
    }

    [TestMethod]
    public void A_lost_connection_empties_everything_and_a_late_reply_is_dropped()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var late = new TaskCompletionSource<string?>();
        _backend.Pending = late;
        service.RefreshWindows();

        _backend.RaiseConnection(false);
        _loop.Process();

        Assert.IsEmpty(service.Monitors);
        Assert.IsEmpty(service.Workspaces);
        Assert.IsEmpty(service.Windows);
        Assert.IsNull(service.FocusedMonitor);
        Assert.IsNull(service.FocusedWorkspace);
        Assert.IsNull(service.ActiveWindow);

        _backend.Pending = null;
        late.SetResult(Clients);
        _loop.Process();
        Assert.IsEmpty(service.Windows);

        _backend.Connected = true;
        _backend.RaiseConnection(true);
        _loop.Process();
        Assert.AreEqual(2, service.Monitors.Count);
    }

    [TestMethod]
    public void A_query_with_no_answer_is_asked_again_while_the_stream_is_up()
    {
        _backend.Connected = true;
        var service = new WinlandService(_loop, Log.Default, _backend);
        using var stopService = service.Stopping();
        _loop.Process();
        Assert.IsEmpty(service.Monitors);

        _backend.Replies["j/get monitors"] = Monitors;
        _loop.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(2, service.Monitors.Count);
    }

    [TestMethod]
    public void A_query_an_event_asks_for_with_no_answer_is_asked_again()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var one = service.Workspaces.Single(w => w.Name == "1");
        _backend.Replies.Remove("j/get workspaces");

        Raise(Event("fullscreen", new { fullscreen = false }));
        Assert.IsFalse(one.HasFullscreen);

        _backend.Replies["j/get workspaces"] = Workspaces;
        _loop.Advance(TimeSpan.FromSeconds(1));

        Assert.IsTrue(one.HasFullscreen);
    }

    [TestMethod]
    public void A_reload_during_a_read_an_event_asked_for_still_reads_the_whole_state()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var late = new TaskCompletionSource<string?>();
        _backend.Pending = late;
        Raise(Event("createworkspace", new { workspace = Workspace(7), monitor = Display1 }));
        _backend.Pending = null;

        _backend.Replies["j/get workspaces"] = """
                                           [{"id":7,"name":"7","monitor":"\\\\.\\DISPLAY1","monitorId":0},
                                            {"id":9,"name":"9","monitor":"\\\\.\\DISPLAY1","monitorId":0}]
                                           """;
        Raise(Event("configreloaded"));
        late.SetResult(Workspaces);
        _loop.Process();

        Assert.Contains(w => w.Name == "9", service.Workspaces);
    }

    [TestMethod]
    public void A_query_with_no_answer_is_not_asked_again_while_Winland_is_down()
    {
        var service = new WinlandService(_loop, Log.Default, _backend);
        using var stopService = service.Stopping();

        service.RefreshWindows();
        _loop.Advance(TimeSpan.FromSeconds(30));

        Assert.AreSequenceEqual(["j/get windows"], _backend.Sent);
    }

    [TestMethod]
    public void A_screen_finds_its_monitor_and_before_the_first_list_one_is_added()
    {
        var screen = new ScreenInfo(@"\\.\DISPLAY1", "DISPLAY1", "", "", "", default, default, 1, 0, 0, 60, true);
        var early = new WinlandService(_loop, Log.Default, _backend);
        using (early.Stopping())
        {
            var added = early.MonitorFor(screen);
            Assert.AreEqual(@"\\.\DISPLAY1", added!.Name);
            Assert.IsNull(early.MonitorFor(null));
        }

        var service = Connected();
        using var stopService = service.Stopping();
        Assert.AreSame(service.Monitors[0], service.MonitorFor(screen));
        Assert.IsNull(service.MonitorFor(screen with { Id = "other", Name = "other" }));
    }

    [TestMethod]
    public void Removing_a_monitor_raises_the_list_and_the_screen_loses_it()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var screen = new ScreenInfo(@"\\.\DISPLAY2", "DISPLAY2", "", "", "", default, default, 1, 0, 0, 60, false);
        var removed = false;
        service.Monitors.CollectionChanged += (_, _) => removed = true;

        Raise(Event("monitorremoved", new { id = 1, name = Display2, description = "Left" }));

        Assert.IsTrue(removed);
        Assert.IsNull(service.MonitorFor(screen));
    }

    [TestMethod]
    public void Dispatch_sends_the_request_and_a_refusal_is_logged()
    {
        var lines = new List<string>();
        var log = new Log { Sink = (_, _, message) => lines.Add(message) };
        var service = new WinlandService(_loop, log, _backend);
        using var stopService = service.Stopping();
        _backend.Replies["workspace 3"] = "ok";
        _backend.Replies["bad"] = "unknown request";

        service.Dispatch("workspace 3");
        service.Dispatch("bad");
        service.Dispatch("nothing");
        _loop.Process();

        Assert.AreSequenceEqual(["workspace 3", "bad", "nothing"], _backend.Sent);
        Assert.AreEqual(2, lines.Count);
        Assert.Contains("unknown request", lines[0], StringComparison.Ordinal);
        Assert.Contains("not running", lines[1], StringComparison.Ordinal);
    }

    [TestMethod]
    public void Activating_a_workspace_on_another_monitor_focuses_that_monitor_first()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        _backend.Replies["focus-monitor 1"] = "ok";
        _backend.Sent.Clear();

        service.Workspaces.First(w => w.Name == "3" && w.Monitor!.Id == 1).Activate();
        _loop.Process();
        service.Workspaces.First(w => w.Name == "3" && w.Monitor!.Id == 0).Activate();
        service.Workspaces.First(w => w.Name == "special:special").Activate();

        Assert.AreSequenceEqual(
        [
            "focus-monitor 1",
            "workspace 3",
            "workspace 3",
            "workspace special:special"
        ], _backend.Sent);
    }

    [TestMethod]
    [DataRow(0, 3, new[] { "workspace 3" })]
    [DataRow(0, 5, new[] { "workspace 5" })]
    [DataRow(1, 3, new[] { "focus-monitor 1", "workspace 3" })]
    [DataRow(1, 5, new[] { "focus-monitor 1", "workspace 5" })]
    public void A_monitor_shows_an_existing_or_missing_workspace(int monitorId, int number, string[] requests)
    {
        var service = Connected();
        using var stopService = service.Stopping();
        _backend.Replies["focus-monitor 1"] = "ok";
        _backend.Sent.Clear();

        service.Monitors.Single(m => m.Id == monitorId).ActivateWorkspace(number);
        _loop.Process();

        Assert.AreSequenceEqual(requests, _backend.Sent);
    }

    [TestMethod]
    public void A_workspace_waits_for_its_monitor_to_take_focus_and_not_after_a_refusal()
    {
        var lines = new List<string>();
        var log = new Log { Sink = (_, _, message) => lines.Add(message) };
        var service = new WinlandService(_loop, log, _backend);
        using var stopService = service.Stopping();
        var screen = new ScreenInfo(@"\\.\DISPLAY2", "DISPLAY2", "", "", "", default, default, 1, 0, 0, 60, false);
        var focusing = new TaskCompletionSource<string?>();
        _backend.Pending = focusing;

        service.MonitorFor(screen)!.ActivateWorkspace(5);
        _loop.Process();
        Assert.AreSequenceEqual([@"focus-monitor \\.\DISPLAY2"], _backend.Sent);

        _backend.Pending = null;
        focusing.SetResult("error: monitor not found");
        _loop.Process();

        Assert.AreSequenceEqual([@"focus-monitor \\.\DISPLAY2"], _backend.Sent);
        Assert.Contains("monitor not found", Assert.ContainsSingle(lines), StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void A_monitor_rejects_a_nonpositive_workspace_number(int number)
    {
        var service = Connected();
        using var stopService = service.Stopping();
        _backend.Sent.Clear();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.Monitors[0].ActivateWorkspace(number));

        Assert.IsEmpty(_backend.Sent);
    }

    [TestMethod]
    public void The_pipe_path_is_what_Winland_says()
    {
        var service = new WinlandService(_loop, Log.Default, _backend);
        using var stopService = service.Stopping();

        Assert.AreEqual(@"\\.\pipe\winland-test", service.PipePath);
    }

    [TestMethod]
    public void The_process_on_the_pipe_is_announced_as_Winland_comes_and_goes()
    {
        var service = new WinlandService(_loop, Log.Default, _backend);
        using var stopService = service.Stopping();
        var changes = Watch(service);

        _backend.ProcessId = 4321;
        _backend.RaiseConnection(true);
        _loop.Process();
        Assert.AreEqual(4321, service.ProcessId);
        Assert.Contains(nameof(WinlandService.ProcessId), changes);

        changes.Clear();
        _backend.ProcessId = 0;
        _backend.RaiseConnection(false);
        _loop.Process();
        Assert.AreEqual(0, service.ProcessId);
        Assert.Contains(nameof(WinlandService.ProcessId), changes);
    }

    [TestMethod]
    public void The_files_Winland_reports_are_read_as_it_connects_and_on_reload_and_dropped_as_it_goes()
    {
        _backend.Replies["j/get files"] = """{"config":"D:\\work.conf","log":null}""";
        var service = Connected();
        using var stopService = service.Stopping();
        Assert.AreEqual(@"D:\work.conf", service.ConfigPath);
        Assert.IsNull(service.LogPath);

        _backend.Replies["j/get files"] = """{"config":"D:\\work.conf","log":"D:\\state\\winland.log"}""";
        Raise(Event("configreloaded"));
        Assert.AreEqual(@"D:\state\winland.log", service.LogPath);

        _backend.RaiseConnection(false);
        _loop.Process();
        Assert.IsNull(service.ConfigPath);
        Assert.IsNull(service.LogPath);
    }

    [TestMethod]
    public void A_Winland_that_does_not_answer_files_reports_none()
    {
        _backend.Replies["j/get files"] = "error: unknown request";
        var service = Connected();
        using var stopService = service.Stopping();

        Assert.IsNull(service.ConfigPath);
        Assert.IsNull(service.LogPath);
        Assert.ContainsSingle(sent => sent == "j/get files", _backend.Sent);
    }

    [TestMethod]
    public void A_disposed_service_stops_following_the_stream()
    {
        var service = Connected();
        var title = service.Windows[0].Title;

        _backend.Raise(Event("windowtitle", new { address = "0x10", title = "Late" }));
        service.Stop();
        _loop.Process();

        Assert.AreEqual(title, service.Windows[0].Title);
        Assert.AreEqual(0, _backend.Listeners);
    }

    private sealed class FakeBackend : IWinlandBackend
    {
        public bool Connected { get; set; }

        public Dictionary<string, string> Replies { get; } = [];

        public List<string> Sent { get; } = [];

        public TaskCompletionSource<string?>? Pending { get; set; }

        private readonly List<(Action<bool> Connection, Action<string> Event)> _listeners = [];

        public int Listeners => _listeners.Count;

        public bool IsConnected => Connected;

        public string PipePath => @"\\.\pipe\winland-test";

        public int ProcessId { get; set; }

        public IDisposable Listen(Action<bool> connectionChanged, Action<string> eventReceived,
            Action<Exception> failed)
        {
            var listener = (connectionChanged, eventReceived);
            _listeners.Add(listener);
            return new DisposeAction(() => _listeners.Remove(listener));
        }

        public Task<string?> RequestAsync(string command)
        {
            Sent.Add(command);
            return Pending?.Task ?? Task.FromResult(Replies.GetValueOrDefault(command));
        }

        public void Raise(string line)
        {
            foreach (var listener in _listeners.ToList())
                listener.Event(line);
        }

        public void RaiseConnection(bool connected)
        {
            Connected = connected;
            foreach (var listener in _listeners.ToList())
                listener.Connection(connected);
        }
    }
}
