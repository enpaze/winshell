using System.ComponentModel;
using Winshell.AppWindows;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class AppWindowsServiceTests
{
    private const string Terminal = "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App";

    private readonly ManualEventLoop _loop = new();
    private readonly FakeWindowBackend _windows = new();

    private AppWindowsService Start(VirtualDesktopsService? desktops = null)
    {
        return new AppWindowsService(_loop, Log.Default, desktops, _windows);
    }

    private static List<string> Watch(INotifyPropertyChanged source)
    {
        var changes = new List<string>();
        source.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        return changes;
    }

    [TestMethod]
    public void A_new_window_goes_at_the_end_and_a_window_that_stays_is_the_same_object()
    {
        _windows.Show(1);
        _windows.Show(2);
        var list = Start();
        using var stopList = list.Stopping();
        var kept = list.Windows[1];

        _windows.Destroy(1);
        _windows.Show(3);
        _loop.Process();

        Assert.AreSequenceEqual([2, 3], list.Windows.Select(window => (int)window.Hwnd));
        Assert.AreSame(kept, list.Windows[0]);
    }

    [TestMethod]
    [DataRow(false, true, false, false, false, false, false, false, true, false)]
    [DataRow(true, false, false, false, false, false, false, false, true, false)]
    [DataRow(true, true, true, false, false, false, false, false, true, false)]
    [DataRow(true, true, false, true, false, false, false, false, true, false)]
    [DataRow(true, true, false, false, false, true, false, false, true, false)]
    [DataRow(true, true, false, false, false, false, true, false, true, false)]
    [DataRow(true, true, false, false, false, false, false, true, true, false)]
    [DataRow(true, true, false, false, false, false, false, false, false, false)]
    [DataRow(true, true, false, false, false, false, false, false, true, true)]
    public void Windows_the_taskbar_skips_stay_out_of_the_list(
        bool exists, bool visible, bool shell, bool child, bool appWindow, bool owned, bool tool, bool noActivate,
        bool title, bool cloaked)
    {
        _windows.Show(1, FakeWindowBackend.Window(),
            new TaskbarWindowFacts(exists, visible, shell, child, appWindow, owned, tool, noActivate, title, cloaked));
        _windows.Show(2);

        var list = Start();
        using var stopList = list.Stopping();

        Assert.AreSequenceEqual([2], list.Windows.Select(window => (int)window.Hwnd));
    }

    [TestMethod]
    public void An_owned_window_that_asks_for_a_taskbar_button_is_listed()
    {
        _windows.Show(1, FakeWindowBackend.Window(),
            FakeWindowBackend.Normal with { AppWindow = true, Owned = true, ToolWindow = true, NoActivate = true });

        var list = Start();
        using var stopList = list.Stopping();

        Assert.AreSequenceEqual([1], list.Windows.Select(window => (int)window.Hwnd));
    }

    [TestMethod]
    public void A_change_reports_only_the_properties_that_differ()
    {
        _windows.Show(1, "Before");
        var list = Start();
        using var stopList = list.Stopping();
        var window = list.Windows[0];
        var changes = Watch(window);

        _windows.Show(1, FakeWindowBackend.Window("After") with { Maximized = true });
        _loop.Process();

        Assert.AreSequenceEqual([nameof(AppWindow.Title), nameof(AppWindow.Maximized)], changes);
        Assert.AreEqual("After", window.Title);
        Assert.IsTrue(window.Maximized);
    }

    [TestMethod]
    public void Title_changes_before_the_loop_turns_raise_Title_once_with_the_last_value()
    {
        _windows.Show(1, "First");
        var list = Start();
        using var stopList = list.Stopping();
        var window = list.Windows[0];
        var changes = Watch(window);

        _windows.Show(1, "Second");
        _windows.Show(1, "Third");
        _windows.Show(1, "Last");
        _loop.Process();

        Assert.AreSequenceEqual([nameof(AppWindow.Title)], changes);
        Assert.AreEqual("Last", window.Title);
    }

    [TestMethod]
    public void A_window_opened_and_closed_before_the_loop_turns_never_appears()
    {
        var list = Start();
        using var stopList = list.Stopping();
        var listChanges = 0;
        list.Windows.CollectionChanged += (_, _) => listChanges++;

        _windows.Show(1);
        _windows.Destroy(1);
        _loop.Process();

        Assert.AreEqual(0, listChanges);
    }

    [TestMethod]
    public void A_window_that_changes_while_another_is_read_is_listed_afterwards()
    {
        _windows.Show(1);
        var list = Start();
        using var stopList = list.Stopping();
        _windows.Reading = hwnd =>
        {
            if (hwnd == 1 && !_windows.Shown.ContainsKey(2))
                _windows.Show(2);
        };

        _windows.Show(1, "Changed");
        _loop.Process();

        Assert.AreSequenceEqual([1, 2], list.Windows.Select(window => (int)window.Hwnd));
    }

    [TestMethod]
    public void A_closed_window_leaves_the_list_before_it_says_so()
    {
        _windows.Show(1);
        _windows.Foreground = 1;
        var list = Start();
        using var stopList = list.Stopping();
        var window = list.Windows[0];
        Assert.AreSame(window, list.ActiveWindow);
        var stillListed = true;
        window.Closed += () => stillListed = list.Windows.Contains(window);

        _windows.Foreground = 0;
        _windows.Destroy(1);
        _loop.Process();

        Assert.IsFalse(stillListed);
        Assert.IsNull(list.ActiveWindow);
    }

    [TestMethod]
    public void An_owned_dialog_activates_its_owner_and_its_flash_shows_on_the_owner_until_then()
    {
        _windows.Show(1);
        _windows.Show(3);
        _windows.Owners[2] = 1;
        _windows.Foreground = 3;
        var list = Start();
        using var stopList = list.Stopping();
        var owner = list.Windows[0];
        var other = list.Windows[1];
        Assert.IsTrue(other.Activated);

        _windows.Flash(2);
        _loop.Process();

        Assert.IsTrue(owner.Attention);
        Assert.IsFalse(owner.Activated);

        _windows.Focus(2);
        _loop.Process();

        Assert.AreSame(owner, list.ActiveWindow);
        Assert.IsTrue(owner.Activated);
        Assert.IsFalse(owner.Attention);
        Assert.IsFalse(other.Activated);
    }

    [TestMethod]
    public void A_window_without_an_id_uses_its_package_or_program_until_it_sets_one()
    {
        var chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        _windows.Show(1, FakeWindowBackend.Window() with { PackageAppId = Terminal });
        _windows.Show(2, FakeWindowBackend.Window() with { Executable = chrome });
        var list = Start();
        using var stopList = list.Stopping();
        var packaged = list.Windows[0];
        var program = list.Windows[1];

        Assert.AreEqual(Terminal, packaged.AppId);
        Assert.AreEqual(@"shell:AppsFolder\" + Terminal, packaged.Icon);
        Assert.AreEqual("chrome", program.AppId);
        Assert.AreEqual(chrome, program.Icon);

        _windows.Show(1, FakeWindowBackend.Window() with { PackageAppId = "Other_8wekyb3d8bbwe!App" });
        _windows.Show(2, FakeWindowBackend.Window() with { AppUserModelId = Terminal });
        _loop.Process();

        Assert.AreEqual(Terminal, packaged.AppId);
        Assert.AreEqual(Terminal, program.AppId);
        Assert.AreEqual(@"shell:AppsFolder\" + Terminal, program.Icon);
        Assert.AreEqual(chrome, program.Executable);
    }

    [TestMethod]
    public void A_parent_is_the_owner_when_the_owner_is_listed()
    {
        _windows.Show(2);
        _windows.Show(3);
        _windows.Show(1);
        _windows.Owners[2] = 1;
        _windows.Owners[3] = 9;
        var list = Start();
        using var stopList = list.Stopping();

        Assert.AreSame(list.Windows.Single(window => window.Hwnd == 1), list.Windows[0].Parent);
        Assert.IsNull(list.Windows[1].Parent);
    }

    [TestMethod]
    public void Setting_a_state_asks_Windows_and_the_value_follows_what_Windows_reports()
    {
        _windows.Show(1);
        var list = Start();
        using var stopList = list.Stopping();
        var window = list.Windows[0];

        window.Maximized = true;
        window.Minimized = true;

        Assert.AreSequenceEqual(["maximize:1:True", "minimize:1:True"], _windows.Calls);
        Assert.IsFalse(window.Maximized);
        Assert.IsFalse(window.Minimized);
    }

    [TestMethod]
    public void Requests_go_to_that_window()
    {
        _windows.Show(7);
        var list = Start();
        using var stopList = list.Stopping();
        var window = list.Windows[0];
        var screen = new ScreenInfo("M2", "", "", "", "", default, default, 1, 0, 0, 60, false);

        window.Activate();
        window.Close();
        window.MoveTo(screen);

        Assert.AreSequenceEqual(["activate:7", "close:7", "move:7:M2"], _windows.Calls);
    }

    [TestMethod]
    public void A_window_is_on_the_screen_Windows_says_is_nearest()
    {
        var screen = new ScreenInfo("M1", "", "", "", "", default, default, 1, 0, 0, 60, true);
        _windows.Screens["M1"] = screen;
        _windows.Show(1);
        _windows.Show(2, FakeWindowBackend.Window() with { Monitor = "gone" });
        var list = Start();
        using var stopList = list.Stopping();
        var changes = Watch(list.Windows[1]);

        Assert.AreSame(screen, Assert.ContainsSingle(list.Windows[0].Screens));
        Assert.IsEmpty(list.Windows[1].Screens);

        _windows.Show(2);
        _loop.Process();

        Assert.AreSequenceEqual([nameof(AppWindow.Screens)], changes);
        Assert.AreSame(screen, Assert.ContainsSingle(list.Windows[1].Screens));
    }

    [TestMethod]
    public void A_window_finds_its_desktop_and_moving_it_reports_the_new_one()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var desktops = new VirtualDesktopsService(_loop, Log.Default, FakeDesktops.Holding(first, first, second));
        using var stopDesktops = desktops.Stopping();
        _windows.Show(1);
        _windows.DesktopIds[1] = first.ToString("D");
        var list = Start(desktops);
        using var stopList = list.Stopping();
        var window = list.Windows[0];
        var changes = Watch(window);
        Assert.AreSame(desktops.Desktops[0], window.Desktop);

        window.MoveToDesktop(desktops.Desktops[1]);

        Assert.AreSequenceEqual([nameof(AppWindow.DesktopId), nameof(AppWindow.Desktop)], changes);
        Assert.AreSame(desktops.Desktops[1], window.Desktop);
    }

    [TestMethod]
    public void A_window_says_its_desktop_may_have_changed_after_each_read_of_the_list()
    {
        var only = Guid.NewGuid();
        var source = FakeDesktops.Holding(only, only);
        var desktops = new VirtualDesktopsService(_loop, Log.Default, source);
        using var stopDesktops = desktops.Stopping();
        _windows.Show(1);
        var list = Start(desktops);
        using var stopList = list.Stopping();
        var changes = Watch(list.Windows[0]);

        desktops.Create();

        Assert.AreSequenceEqual([nameof(AppWindow.DesktopId), nameof(AppWindow.Desktop)], changes);
    }

    [TestMethod]
    public void Without_a_desktop_list_a_window_still_has_a_desktop_id()
    {
        _windows.Show(1);
        _windows.DesktopIds[1] = "id";
        var list = Start();
        using var stopList = list.Stopping();

        Assert.AreEqual("id", list.Windows[0].DesktopId);
        Assert.IsNull(list.Windows[0].Desktop);
    }

    [TestMethod]
    public void A_disposed_service_stops_following_the_windows()
    {
        var list = Start();

        _windows.Show(1);
        list.Stop();
        _loop.Process();

        Assert.IsEmpty(list.Windows);
        Assert.IsFalse(_windows.HasListeners);
    }
}
