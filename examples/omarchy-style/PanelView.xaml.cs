using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Winshell;
using Winshell.Network;
using Winshell.Power;
using Winshell.Tray;

namespace OmarchyStyle;

/// <summary>
/// The panel on one screen: the Windows glyph and Winland's workspaces on the left, the clock in the middle, and the
/// tray and the status of Bluetooth, the network, audio, the screen and the battery on the right.
/// </summary>
public sealed partial class PanelView : UserControl, IDisposable
{
    /// <summary>The panel's height, which it also reserves.</summary>
    public const double Size = 26;

    private const double Slot = 27;
    private const double IconSize = 12;

    private readonly Shell _shell;
    private readonly ScreenInfo _screen;
    private readonly List<Watcher> _watchers = [];
    private readonly DispatcherQueueTimer _clock;
    private readonly Style _toolTip;
    private readonly Brush _foreground;
    private readonly Brush _alert;
    private int _overflowCount;
    private bool _overflowOpen;

    public PanelView(Shell shell, ScreenInfo screen)
    {
        InitializeComponent();
        _shell = shell;
        _screen = screen;
        _toolTip = (Style)Resources["PanelToolTip"];
        _foreground = (Brush)Resources["PanelForeground"];
        _alert = (Brush)Resources["PanelAlert"];

        _clock = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _clock.IsRepeating = false;
        _clock.Tick += (_, _) => ShowTime();
        ShowTime();

        _watchers.Add(new Watcher(ShowWorkspaces));
        if (screen.IsPrimary)
            _watchers.Add(new Watcher(ShowTray));
        _watchers.Add(new Watcher(ShowBluetooth));
        _watchers.Add(new Watcher(ShowNetwork));
        _watchers.Add(new Watcher(ShowAudio));
        ShowScreen();
        _watchers.Add(new Watcher(ShowBattery));
    }

    public void Dispose()
    {
        _clock.Stop();
        foreach (var watcher in _watchers)
            watcher.Dispose();
    }

    // Left

    private void ShowWorkspaces(Watcher watch)
    {
        var winland = watch.Read(_shell.Winland);
        Workspaces.Children.Clear();
        // Winland numbers workspaces per monitor, so the panel shows its own screen's: 1 to 5 always, and any other
        // that exists. None while Winland isn't running or has no monitor for this screen.
        if ((winland.ProcessId == 0 ? null : winland.MonitorFor(_screen)) is not { } monitor)
        {
            Workspaces.Visibility = Visibility.Collapsed;
            return;
        }

        Workspaces.Visibility = Visibility.Visible;
        watch.Read(monitor);
        var own = watch.Read(winland.Workspaces).Where(w => watch.Read(w).Monitor == monitor && w.Id > 0).ToList();
        foreach (var id in own.Select(w => w.Id).Concat(Enumerable.Range(1, 5)).Distinct().Order())
        {
            var workspace = own.Find(w => w.Id == id);
            var shown = monitor.ActiveWorkspace?.Id == id;
            var occupied = workspace is not null && watch.Read(workspace.Windows).Count > 0;
            var button = new Button
            {
                Style = (Style)Resources["WorkspaceButton"],
                Content = shown ? Glyphs.Tiling : id.ToString(CultureInfo.CurrentCulture),
                Opacity = shown || occupied ? 1 : 0.5
            };
            AutomationProperties.SetName(button, "Workspace " + id.ToString(CultureInfo.CurrentCulture));
            button.Click += (_, _) =>
            {
                if (workspace is not null)
                    workspace.Activate();
                else if (monitor.Focused)
                    winland.Dispatch("workspace " + id.ToString(CultureInfo.InvariantCulture));
                else
                    // A workspace that doesn't exist yet can only be made on the focused monitor, and Winshell can't
                    // yet focus another and show it in one request (https://github.com/enpaze/winshell/issues/1694).
                    // This click focuses the monitor; the next one shows the workspace.
                    winland.Dispatch("focusmonitor " + monitor.Id.ToString(CultureInfo.InvariantCulture));
            };
            Workspaces.Children.Add(button);
        }
    }

    // Centre

    private void ShowTime()
    {
        var now = DateTime.Now;
        Clock.Text = now.ToString("dddd HH:mm", CultureInfo.CurrentCulture);
        SetToolTip(Clock, now.ToString("D", CultureInfo.CurrentCulture));
        _clock.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
        _clock.Start();
    }

    // Right

    private void ShowTray(Watcher watch)
    {
        var items = watch.Read(_shell.SystemTray.Items).Select(watch.Read).ToList();
        PinnedItems.Children.Clear();
        OverflowItems.Children.Clear();
        foreach (var item in items)
            (item.Overflow ? OverflowItems : PinnedItems).Children.Add(TrayButton(item));
        _overflowCount = OverflowItems.Children.Count;
        Overflow.Visibility = _overflowCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        Tray.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_overflowOpen)
            SlideOverflow(_overflowCount * Slot);
    }

    private Button TrayButton(SystemTrayItem item)
    {
        var icon = item.IconAt((int)Math.Round(IconSize * _screen.DevicePixelRatio)) ?? item.Icon;
        var button = new Button
        {
            Style = (Style)Resources["PanelButton"],
            Content = new Image
            {
                Width = IconSize,
                Height = IconSize,
                Source = icon is null ? null : TrayIcons.ToBitmap(icon)
            }
        };
        AutomationProperties.SetName(button, item.AppName);
        SetToolTip(button, item.Tooltip.Length > 0 ? item.Tooltip : item.AppName);
        button.Click += (_, _) => item.Click();
        button.RightTapped += (_, _) => item.Display();
        return button;
    }

    private void OnOverflowEntered(object sender, RoutedEventArgs e)
    {
        _overflowOpen = true;
        SlideOverflow(_overflowCount * Slot);
    }

    private void OnOverflowExited(object sender, RoutedEventArgs e)
    {
        _overflowOpen = false;
        SlideOverflow(0);
    }

    private void SlideOverflow(double width)
    {
        var slide = new DoubleAnimation
        {
            To = width,
            Duration = TimeSpan.FromMilliseconds(600),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(slide, OverflowClip);
        Storyboard.SetTargetProperty(slide, nameof(Width));
        var story = new Storyboard();
        story.Children.Add(slide);
        story.Begin();
    }

    private void ShowBluetooth(Watcher watch)
    {
        var bluetooth = watch.Read(_shell.Bluetooth);
        var adapter = bluetooth.DefaultAdapter;
        if (adapter is null)
        {
            Bluetooth.Visibility = Visibility.Collapsed;
            return;
        }

        watch.Read(adapter);
        var connected = watch.Read(bluetooth.Devices).Where(d => watch.Read(d).Connected).Select(d => d.Name).ToList();
        Bluetooth.Visibility = Visibility.Visible;
        if (!adapter.Enabled)
            Show(Bluetooth, Glyphs.BluetoothOff, "Bluetooth off");
        else if (connected.Count > 0)
            Show(Bluetooth, Glyphs.BluetoothConnected, string.Join('\n', connected));
        else
            Show(Bluetooth, Glyphs.BluetoothOn, "Bluetooth on");
    }

    private void ShowNetwork(Watcher watch)
    {
        var networking = watch.Read(_shell.Networking);
        // The connection in use: a connected adapter, wired before Wi-Fi.
        var device = watch.Read(networking.Devices)
            .Where(d => watch.Read(d).Connected)
            .OrderBy(d => d.Type == DeviceType.Wired ? 0 : 1)
            .FirstOrDefault();
        // Profiles also lists saved Wi-Fi profiles that aren't connected
        // (https://github.com/enpaze/winshell/issues/1696), so the panel takes the one that reaches a network.
        var profile = device is null
            ? null
            : watch.Read(networking.Profiles).Select(watch.Read)
                .FirstOrDefault(p => p.AdapterId == device.Id && p.Connectivity > NetworkConnectivity.None);
        var portal = networking.Connectivity == NetworkConnectivity.CaptivePortal;
        var restricted = device is not null && (portal || networking.Connectivity == NetworkConnectivity.LocalAccess);
        var name = profile?.Name ?? device?.Name;
        Show(
            Network,
            Glyphs.Network(device?.Type ?? DeviceType.None, profile?.SignalStrength ?? 0, restricted),
            name is null ? "Disconnected" : portal ? name + ": sign in to the network" : restricted ? name + ": no internet" : name,
            restricted);
    }

    private void ShowAudio(Watcher watch)
    {
        var device = watch.Read(_shell.Audio).DefaultPlaybackDevice;
        if (device is null)
        {
            Audio.Visibility = Visibility.Collapsed;
            return;
        }

        watch.Read(device);
        Audio.Visibility = Visibility.Visible;
        var level = device.Muted ? "muted" : Math.Round(device.Volume * 100).ToString(CultureInfo.CurrentCulture) + "%";
        Show(Audio, Glyphs.Audio(device), device.Description + ": " + level);
    }

    private void ShowScreen()
    {
        // A change of screens makes every panel again, so this is read once.
        var several = _shell.Screens.Count > 1;
        Show(Screen, several ? Glyphs.Screens : Glyphs.Screen, _screen.Model.Length > 0 ? _screen.Model : _screen.Name);
    }

    private void ShowBattery(Watcher watch)
    {
        var power = watch.Read(_shell.Power);
        var battery = watch.Read(power.DisplayDevice);
        Battery.Visibility = battery.IsPresent ? Visibility.Visible : Visibility.Collapsed;
        if (!battery.IsPresent)
            return;

        var state = power.OnBattery ? "On battery"
            : battery.State == PowerDeviceState.FullyCharged ? "Fully charged"
            : battery.State == PowerDeviceState.NotCharging ? "Not charging"
            : "Charging";
        var percent = Math.Round(battery.Percentage * 100).ToString(CultureInfo.CurrentCulture);
        Show(Battery, Glyphs.Power(battery, power.OnBattery), percent + "% · " + state);
    }

    private void OnBluetoothClick(object sender, RoutedEventArgs e)
    {
        OpenSettings("bluetooth");
    }

    private void OnNetworkClick(object sender, RoutedEventArgs e)
    {
        OpenSettings("network-status");
    }

    private void OnAudioClick(object sender, RoutedEventArgs e)
    {
        OpenSettings("sound");
    }

    private void OnScreenClick(object sender, RoutedEventArgs e)
    {
        OpenSettings("display");
    }

    private void OnBatteryClick(object sender, RoutedEventArgs e)
    {
        OpenSettings("powersleep");
    }

    private static void OpenSettings(string page)
    {
        Process.Start(new ProcessStartInfo("ms-settings:" + page) { UseShellExecute = true })?.Dispose();
    }

    private void Show(Button button, string glyph, string toolTip, bool alert = false)
    {
        button.Content = glyph;
        button.Foreground = alert ? _alert : _foreground;
        AutomationProperties.SetName(button, toolTip);
        SetToolTip(button, toolTip);
    }

    private void SetToolTip(DependencyObject element, string text)
    {
        if (ToolTipService.GetToolTip(element) is ToolTip tip)
            tip.Content = text;
        else
            ToolTipService.SetToolTip(element, new ToolTip { Content = text, Style = _toolTip });
    }
}
