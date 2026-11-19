using System.ComponentModel;
using Winshell.Tray;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class TrayServiceTests
{
    private const nint App = 0x100;
    private readonly ManualEventLoop _loop = new();
    private readonly FakeTrayBackend _backend = new() { Cursor = (5, 6) };

    public TrayServiceTests()
    {
        _backend.Windows[App] = 42;
    }

    private int Broadcasts => _backend.Calls.Count(call => call == "broadcast");

    private TrayService Tray()
    {
        var tray = new TrayService(_loop, Log.Default, _backend);
        _loop.Process();
        return tray;
    }

    private static NotifyIconCall Call(uint message, uint flags = 0)
    {
        return new NotifyIconCall { Message = message, Flags = flags, Window = App, Id = 1 };
    }

    private static NotifyIconCall Add()
    {
        return Call(TrayProtocol.Add, TrayProtocol.NIF_TIP) with { Tip = "Tip" };
    }

    private static NotifyIconCall Hide(bool hidden)
    {
        return Call(TrayProtocol.Modify, TrayProtocol.NIF_STATE) with
        {
            State = hidden ? TrayProtocol.NIS_HIDDEN : 0,
            StateMask = TrayProtocol.NIS_HIDDEN
        };
    }

    private static List<string> Watch(INotifyPropertyChanged source)
    {
        var changes = new List<string>();
        source.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        return changes;
    }

    [TestMethod]
    public void An_icon_keeps_its_item_and_only_what_differs_changes()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        _backend.Send(Add());
        var item = tray.Items[0];
        var changes = Watch(item);

        var image = new PixelImage(1, 1, new byte[4]);
        _backend.Images[0x42] = image;
        var change = Call(TrayProtocol.Modify, TrayProtocol.NIF_TIP | TrayProtocol.NIF_ICON) with
        {
            Tip = "Changed",
            Icon = 0x42
        };
        _backend.Send(change);
        _backend.Send(change);

        Assert.AreSame(item, Assert.ContainsSingle(tray.Items));
        Assert.AreSequenceEqual([nameof(SystemTrayItem.Icon), nameof(SystemTrayItem.Tooltip)], changes);
        Assert.AreEqual("Changed", item.Tooltip);
        Assert.AreSame(image, item.Icon);
    }

    [TestMethod]
    public void An_icon_the_app_hides_leaves_the_list_until_it_shows_it_again()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        _backend.Send(Add());

        _backend.Send(Hide(true));
        Assert.IsEmpty(tray.Items);

        _backend.Send(Hide(false));
        Assert.ContainsSingle(tray.Items);

        _backend.Send(Call(TrayProtocol.Delete));
        Assert.IsEmpty(tray.Items);
    }

    [TestMethod]
    public void An_item_is_ready_once_it_is_listed()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        var ready = 0;
        _backend.Send(Add());
        tray.Items[0].Ready += () => ready++;

        _loop.Process();
        _loop.Process();

        Assert.AreEqual(1, ready);
    }

    [TestMethod]
    [DataRow(0u, BalloonIcon.None)]
    [DataRow(2u | 0x10u, BalloonIcon.Warning)]
    [DataRow(4u, BalloonIcon.App)]
    [DataRow(9u, BalloonIcon.None)]
    public void A_balloon_reaches_its_item(uint flags, BalloonIcon expected)
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        _backend.Send(Add());
        var balloons = new List<(string, string, BalloonIcon)>();
        tray.Items[0].Balloon += (title, message, kind) => balloons.Add((title, message, kind));

        _backend.Send(Call(TrayProtocol.Modify, TrayProtocol.NIF_INFO) with
        {
            InfoTitle = "Title",
            Info = "Message",
            InfoFlags = flags
        });

        Assert.AreEqual(("Title", "Message", expected), Assert.ContainsSingle(balloons));
    }

    [TestMethod]
    public void Clicks_reach_the_app_with_the_point_they_name()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        _backend.Send(Call(TrayProtocol.Add, TrayProtocol.NIF_MESSAGE) with { CallbackMessage = 0x8001 });
        _backend.Send(Call(TrayProtocol.SetVersion) with { TimeoutOrVersion = TrayProtocol.Version4 });
        var item = tray.Items[0];

        Action[] clicks =
        [
            item.Click,
            item.DoubleClick,
            item.MiddleClick,
            item.Display,
            () => item.Display(10, 20)
        ];

        Assert.AreSequenceEqual(
        [
            "201 202 400 at 5,6",
            "201 202 400 203 202 400 at 5,6",
            "207 208 at 5,6",
            "204 205 7b at 5,6",
            "204 205 7b at 10,20"
        ], clicks.Select(Clicked));
    }

    private string Clicked(Action click)
    {
        _backend.Notified.Clear();
        click();
        var point = _backend.Notified[0].WParam;
        return
            $"{string.Join(" ", _backend.Notified.Select(sent => $"{sent.LParam & 0xffff:x}"))} at {point & 0xffff},{point >> 16}";
    }

    [TestMethod]
    public void A_system_message_is_never_an_icons_callback()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        _backend.Send(Call(TrayProtocol.Add, TrayProtocol.NIF_MESSAGE) with { CallbackMessage = 0x8001 });
        tray.Items[0].Click();
        foreach (var sent in _backend.Notified)
            Assert.AreEqual(0x8001u, sent.Message);
        Assert.IsNotEmpty(_backend.Notified);

        _backend.Send(Call(TrayProtocol.Modify, TrayProtocol.NIF_MESSAGE) with { CallbackMessage = 0x0010 });
        _backend.Notified.Clear();
        tray.Items[0].Click();
        Assert.IsEmpty(_backend.Notified);
    }

    [TestMethod]
    public void An_icon_with_no_resource_is_the_apps_own_bitmap_at_any_size()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        var image = new PixelImage(1, 1, new byte[4]);
        _backend.Images[0x42] = image;
        _backend.Send(Call(TrayProtocol.Add, TrayProtocol.NIF_ICON) with { Icon = 0x42 });

        Assert.AreSame(image, tray.Items[0].IconAt(32));
    }

    [TestMethod]
    public void Changing_an_icons_bitmap_raises_the_icon()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        var changed = new PixelImage(1, 1, new byte[4]);
        _backend.Images[0x42] = new PixelImage(1, 1, new byte[4]);
        _backend.Images[0x43] = changed;
        _backend.Send(Call(TrayProtocol.Add, TrayProtocol.NIF_ICON) with { Icon = 0x42 });
        var item = tray.Items[0];
        var changes = Watch(item);

        _backend.Send(Call(TrayProtocol.Modify, TrayProtocol.NIF_ICON) with { Icon = 0x43 });

        Assert.Contains(nameof(SystemTrayItem.Icon), changes);
        Assert.AreSame(changed, item.IconAt(32));
    }

    [TestMethod]
    public void An_icon_from_a_resource_is_drawn_again_at_a_new_size()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        var image = new PixelImage(16, 16, new byte[16 * 16 * 4]);
        var sharp = new PixelImage(32, 32, new byte[32 * 32 * 4]);
        var resource = new IconResource(@"C:\Apps\sync.exe", 1, "");
        _backend.Images[0x42] = image;
        _backend.Resources[0x42] = resource;
        _backend.Drawn[(resource, 32)] = sharp;
        _backend.Send(Call(TrayProtocol.Add, TrayProtocol.NIF_ICON) with { Icon = 0x42 });
        var item = tray.Items[0];

        Assert.AreSame(sharp, item.IconAt(32));
        Assert.AreSame(image, item.IconAt(16));
        Assert.AreSame(image, item.IconAt(48));
    }

    [TestMethod]
    public void Windows_settings_move_an_icon_onto_the_taskbar()
    {
        const string program = @"C:\Apps\sync.exe";
        _backend.Programs[App] = program;
        var tray = Tray();
        using var stopTray = tray.Stopping();
        _backend.Send(Add());
        Assert.IsTrue(tray.Items[0].Overflow);

        _backend.Settings.Add(new TrayIconSetting(Guid.Empty, program, 1, true));
        _backend.SignalSettingsChanged();

        Assert.IsFalse(tray.Items[0].Overflow);
    }

    [TestMethod]
    public void A_tray_waits_for_the_instance_that_has_the_notification_area_and_takes_over_when_it_exits()
    {
        _backend.Taken = true;
        var tray = Tray();
        using var stopTray = tray.Stopping();
        Assert.IsFalse(_backend.IsOpen);
        _loop.Advance(TimeSpan.FromSeconds(1));
        Assert.IsFalse(_backend.IsOpen);
        Assert.AreEqual(0, Broadcasts);

        _backend.Taken = false;
        _loop.Advance(TimeSpan.FromMilliseconds(500));
        Assert.IsTrue(_backend.IsOpen);
        Assert.AreEqual(1, Broadcasts);
    }

    [TestMethod]
    public void An_active_tray_has_no_half_second_poll()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        Assert.IsTrue(_backend.IsOpen);
        Assert.AreEqual(TimeSpan.FromSeconds(30), _loop.NextDue - _loop.Now);
    }

    [TestMethod]
    public void An_icon_whose_window_is_gone_leaves_at_the_next_check()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        _backend.Send(Add());
        _backend.Windows.Remove(App);

        _loop.Advance(TimeSpan.FromSeconds(29));
        Assert.ContainsSingle(tray.Items);
        _loop.Advance(TimeSpan.FromSeconds(1));
        Assert.IsEmpty(tray.Items);
    }

    [TestMethod]
    public void Explorer_coming_to_the_front_raises_the_tray_without_waiting_for_a_timer()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        _backend.Calls.Clear();

        _backend.SignalExplorerInFront();

        Assert.AreSequenceEqual(["raise"], _backend.Calls);
    }

    [TestMethod]
    public void A_restarted_explorer_is_answered_once()
    {
        var tray = Tray();
        using var stopTray = tray.Stopping();
        Assert.AreEqual(1, Broadcasts);

        _backend.SignalTaskbarCreated();
        _loop.Advance(TimeSpan.FromSeconds(2));
        Assert.AreEqual(1, Broadcasts);

        _backend.Explorer = 0;
        _loop.Advance(TimeSpan.FromSeconds(1));
        _backend.Explorer = 0x600;
        _backend.SignalTaskbarCreated();
        Assert.AreEqual(1, Broadcasts);
        _loop.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(2, Broadcasts);

        _backend.SignalTaskbarCreated();
        _loop.Advance(TimeSpan.FromSeconds(2));
        Assert.AreEqual(2, Broadcasts);
    }

    [TestMethod]
    public void An_app_bar_message_lowers_each_open_tray_behind_Explorers_taskbar_and_raises_it_again()
    {
        using (Tray().Stopping())
        {
            _backend.Calls.Clear();

            AppBarMessages.Autohide(0);

            Assert.AreSequenceEqual(["lower", "raise"], _backend.Calls);
        }

        _backend.Calls.Clear();
        AppBarMessages.Autohide(0);

        Assert.IsEmpty(_backend.Calls);
    }

    [TestMethod]
    public void A_disposed_tray_gives_the_notification_area_up_and_stops_following_it()
    {
        var tray = Tray();
        _backend.Send(Add());
        var ready = 0;
        tray.Items[0].Ready += () => ready++;
        _backend.Calls.Clear();

        tray.Stop();
        _loop.Advance(TimeSpan.FromSeconds(30));

        Assert.ContainsSingle(tray.Items);
        Assert.AreEqual(0, ready);
        Assert.IsFalse(_backend.IsOpen);
        Assert.AreSequenceEqual(["close", "broadcast", "dispose"], _backend.Calls);
    }

    [TestMethod]
    public void An_image_is_drawn_from_a_local_module_with_straight_alpha()
    {
        var image = IconImage.FromResource(
            new IconResource(Path.Combine(Environment.SystemDirectory, "shell32.dll"), 3, ""), 32);

        Assert.IsNotNull(image);
        Assert.AreEqual(32, image.Width);
        Assert.AreEqual(32 * 32 * 4, image.Bgra.Length);
        Assert.IsFalse(image.Premultiplied);
    }
}
