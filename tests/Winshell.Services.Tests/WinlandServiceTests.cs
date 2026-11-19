using System.ComponentModel;
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
                                      [{"id":3,"name":"3","monitor":"\\\\.\\DISPLAY1","monitorID":0,"hasfullscreen":false},
                                       {"id":1,"name":"1","monitor":"\\\\.\\DISPLAY1","monitorID":0,"hasfullscreen":true},
                                       {"id":3,"name":"3","monitor":"\\\\.\\DISPLAY2","monitorID":1,"hasfullscreen":false},
                                       {"name":"notes","monitor":"\\\\.\\DISPLAY1","monitorID":0},
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
        _backend.Replies["j/monitors"] = Monitors;
        _backend.Replies["j/workspaces"] = Workspaces;
        _backend.Replies["j/clients"] = Clients;
        _backend.Replies["j/activewindow"] = """{"address":"0x10"}""";
        _backend.Connected = true;
        var service = new WinlandService(_loop, Log.Default, _backend);
        _loop.Process();
        return service;
    }

    private void Raise(string line)
    {
        _backend.Raise(line);
        _loop.Process();
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
        _backend.Replies["j/monitors"] = Monitors;
        _backend.Replies["j/workspaces"] = Workspaces;
        _backend.Replies["j/clients"] = Clients;
        _backend.Replies["j/activewindow"] = """{"address":"0x10"}""";
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

        Raise(@"openwindow>>30,1,\\.\DISPLAY1,Browser,Browser");
        Raise(@"openwindow>>40,1,\\.\DISPLAY1,Editor,Second editor");
        Raise(@"openwindow>>50,1,\\.\DISPLAY1,Editor,Third editor");

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

        Raise(@"openwindow>>30,1,\\.\DISPLAY1,Browser,Browser");
        Assert.AreSequenceEqual([@"C:\editor.exe", @"C:\browser.exe"], main.AppIcons);

        Raise(@"movewindowv2>>30,3,3,\\.\DISPLAY1");
        Assert.AreSequenceEqual([@"C:\editor.exe"], main.AppIcons);
        Assert.AreSequenceEqual([@"C:\browser.exe"], three.AppIcons);

        Raise("closewindow>>30");
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

        Raise("windowtitlev2>>10,New, title");

        Assert.AreEqual("New, title", window.Title);
        Assert.AreSequenceEqual([nameof(WinlandWindow.Title)], changes);
        Assert.AreEqual(("windowtitlev2", "New, title"), Assert.ContainsSingle(seen));
        Assert.AreSame(window, service.Windows[0]);
    }

    [TestMethod]
    public void An_event_splits_into_its_arguments()
    {
        var parsed = new WinlandEvent("openwindow", "10,1,a,b,c").Parse(3);

        Assert.AreSequenceEqual(["10", "1", "a,b,c"], parsed);
        Assert.AreSequenceEqual(["x", "", ""], new WinlandEvent("e", "x").Parse(3));
    }

    [TestMethod]
    public void Showing_a_workspace_moves_active_and_focus()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var main = service.FocusedMonitor!;
        var old = service.FocusedWorkspace!;
        var three = service.Workspaces.First(w => w.Name == "3" && w.Monitor == main);

        Raise(@"workspacev2>>3,3,\\.\DISPLAY1");

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

        Raise(@"focusedmon>>\\.\DISPLAY2,3");

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

        Raise(@"openwindow>>30,1,\\.\DISPLAY1,App,Title, with comma");
        var window = service.Windows.Single(t => t.Address == "30");
        Assert.AreEqual("Title, with comma", window.Title);
        Assert.Contains(window, main.Windows);

        Raise(@"movewindowv2>>30,3,3,\\.\DISPLAY1");
        Assert.AreSame(three, window.Workspace);
        Assert.Contains(window, three.Windows);
        Assert.DoesNotContain(window, main.Windows);

        Raise("activewindowv2>>30");
        Assert.AreSame(window, service.ActiveWindow);
        Assert.IsFalse(service.Windows[0].Activated);

        Raise("closewindow>>30");
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

        Raise(@"openwindow>>30,1,\\.\DISPLAY1,App,Notes");

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

        Raise("urgent>>20");
        Assert.IsTrue(other.Urgent);
        Assert.IsTrue(workspace.Urgent);

        Raise(@"focusedmon>>\\.\DISPLAY2,3");
        Assert.IsFalse(other.Urgent);
        Assert.IsFalse(workspace.Urgent);
    }

    [TestMethod]
    public void A_workspace_comes_and_goes()
    {
        var service = Connected();
        using var stopService = service.Stopping();

        Raise(@"createworkspacev2>>7,7,\\.\DISPLAY1");
        var seven = service.Workspaces.Single(w => w.Name == "7");
        Assert.AreEqual(7, seven.Id);

        Raise(@"destroyworkspacev2>>7,7,\\.\DISPLAY1");
        Assert.DoesNotContain(seven, service.Workspaces);
    }

    [TestMethod]
    public void A_monitor_that_goes_leaves_its_workspaces_with_none()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var left = service.Monitors[1];
        var three = left.ActiveWorkspace!;

        Raise(@"monitorremoved>>\\.\DISPLAY2");

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

        _backend.Replies["j/monitors"] = Monitors;
        _loop.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(2, service.Monitors.Count);
    }

    [TestMethod]
    public void A_query_an_event_asks_for_with_no_answer_is_asked_again()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        var one = service.Workspaces.Single(w => w.Name == "1");
        _backend.Replies.Remove("j/workspaces");

        Raise("fullscreen>>0");
        Assert.IsFalse(one.HasFullscreen);

        _backend.Replies["j/workspaces"] = Workspaces;
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
        Raise(@"createworkspacev2>>7,7,\\.\DISPLAY1");
        _backend.Pending = null;

        _backend.Replies["j/workspaces"] = """
                                           [{"id":7,"name":"7","monitor":"\\\\.\\DISPLAY1","monitorID":0},
                                            {"id":9,"name":"9","monitor":"\\\\.\\DISPLAY1","monitorID":0}]
                                           """;
        Raise("configreloaded>>");
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

        Assert.AreSequenceEqual(["j/clients"], _backend.Sent);
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

        Raise(@"monitorremoved>>\\.\DISPLAY2");

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
        _backend.Replies["dispatch workspace 3"] = "ok";
        _backend.Replies["dispatch bad"] = "unknown request";

        service.Dispatch("workspace 3");
        service.Dispatch("bad");
        service.Dispatch("nothing");
        _loop.Process();

        Assert.AreSequenceEqual(["dispatch workspace 3", "dispatch bad", "dispatch nothing"], _backend.Sent);
        Assert.AreEqual(2, lines.Count);
        Assert.Contains("unknown request", lines[0], StringComparison.Ordinal);
        Assert.Contains("not running", lines[1], StringComparison.Ordinal);
    }

    [TestMethod]
    public void Activating_a_workspace_on_another_monitor_focuses_that_monitor_first()
    {
        var service = Connected();
        using var stopService = service.Stopping();
        _backend.Sent.Clear();

        service.Workspaces.First(w => w.Name == "3" && w.Monitor!.Id == 1).Activate();
        service.Workspaces.First(w => w.Name == "3" && w.Monitor!.Id == 0).Activate();
        service.Workspaces.First(w => w.Name == "special:special").Activate();

        Assert.AreSequenceEqual(
        [
            "[[BATCH]]dispatch focusmonitor 1;dispatch workspace 3",
            "dispatch workspace 3",
            "dispatch workspace special:special"
        ], _backend.Sent);
    }

    [TestMethod]
    [DataRow(0, 3, "dispatch workspace 3")]
    [DataRow(0, 5, "dispatch workspace 5")]
    [DataRow(1, 3, "[[BATCH]]dispatch focusmonitor 1;dispatch workspace 3")]
    [DataRow(1, 5, "[[BATCH]]dispatch focusmonitor 1;dispatch workspace 5")]
    public void A_monitor_shows_an_existing_or_missing_workspace_in_one_request(int monitorId, int number,
        string request)
    {
        var service = Connected();
        using var stopService = service.Stopping();
        _backend.Sent.Clear();

        service.Monitors.Single(m => m.Id == monitorId).ActivateWorkspace(number);

        Assert.AreSequenceEqual([request], _backend.Sent);
    }

    [TestMethod]
    public void A_monitor_can_show_a_workspace_before_its_id_is_reported()
    {
        var service = new WinlandService(_loop, Log.Default, _backend);
        using var stopService = service.Stopping();
        var screen = new ScreenInfo(@"\\.\DISPLAY2", "DISPLAY2", "", "", "", default, default, 1, 0, 0, 60, false);

        service.MonitorFor(screen)!.ActivateWorkspace(5);

        Assert.AreSequenceEqual([@"[[BATCH]]dispatch focusmonitor \\.\DISPLAY2;dispatch workspace 5"], _backend.Sent);
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
        _backend.Replies["j/files"] = """{"config":"D:\\work.conf","log":null}""";
        var service = Connected();
        using var stopService = service.Stopping();
        Assert.AreEqual(@"D:\work.conf", service.ConfigPath);
        Assert.IsNull(service.LogPath);

        _backend.Replies["j/files"] = """{"config":"D:\\work.conf","log":"D:\\state\\winland.log"}""";
        Raise("configreloaded>>");
        Assert.AreEqual(@"D:\state\winland.log", service.LogPath);

        _backend.RaiseConnection(false);
        _loop.Process();
        Assert.IsNull(service.ConfigPath);
        Assert.IsNull(service.LogPath);
    }

    [TestMethod]
    public void A_Winland_that_does_not_answer_files_reports_none()
    {
        _backend.Replies["j/files"] = "error: unknown request";
        var service = Connected();
        using var stopService = service.Stopping();

        Assert.IsNull(service.ConfigPath);
        Assert.IsNull(service.LogPath);
        Assert.ContainsSingle(sent => sent == "j/files", _backend.Sent);
    }

    [TestMethod]
    public void A_disposed_service_stops_following_the_stream()
    {
        var service = Connected();
        var title = service.Windows[0].Title;

        _backend.Raise("windowtitlev2>>10,Late");
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
