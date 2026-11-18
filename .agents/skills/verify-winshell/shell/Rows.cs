using System.ComponentModel;
using System.Globalization;
using Winshell.Apps;
using Winshell.Input;
using Winshell.Power;

namespace FeatureMap;

public sealed record RowProps(string Id, INotifyPropertyChanged Watched, Func<string> Describe);

public sealed class Row : Component<RowProps>
{
    public override Element Render()
    {
        UseObservableTree(Props.Watched);
        return TextBlock(Props.Describe()).AutomationId(Props.Id);
    }
}

public sealed record WindowRowProps(string Id, Func<ShellWindow> Window);

public class WindowRow : Component<WindowRowProps>
{
    public override Element Render()
    {
        var window = Props.Window();
        UseObservableTree(window);
        var kind = Props.Id["block.".Length..].Replace('-', ' ');
        return TextBlock($"{kind}: {Size(window.ActualWidth)}x{Size(window.ActualHeight)}").AutomationId(Props.Id);
    }

    private static string Size(double value)
    {
        return Math.Round(value).ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class PanelRow : WindowRow;

public sealed class PopupRow : WindowRow;

internal static class Rows
{
    private static readonly INotifyPropertyChanged Unwatched = new Fixed();

    public static RowProps Launcher(Launched launched)
    {
        return new RowProps("block.launcher", launched, () => launched.Text);
    }

    public static RowProps[] Services(Shell shell, Hotkey hotkey, IdleMonitor idle, AppJumpList jumpList)
    {
        return
        [
            new("service.apps", shell.Apps, () => $"apps: {shell.Apps.Apps.Count}"),
            new("service.audio", shell.Audio, () =>
                $"ready: {shell.Audio.Ready}; playback: {shell.Audio.DefaultPlaybackDevice?.Name ?? "none"}; recording: {shell.Audio.DefaultRecordingDevice?.Name ?? "none"}"),
            new("service.bluetooth", shell.Bluetooth, () =>
                $"ready: {shell.Bluetooth.Ready}; adapter: {shell.Bluetooth.DefaultAdapter?.Name ?? "none"}; devices: {shell.Bluetooth.Devices.Count}"),
            new("service.brightness", shell.Brightness, () =>
                $"ready: {shell.Brightness.Ready}; monitors: {shell.Brightness.Monitors.Count}"),
            new("service.cpu", shell.Cpu, () => $"cpu: {shell.Cpu.Usage.ToString("0", CultureInfo.InvariantCulture)}%"),
            new("service.explorer-taskbar", shell.ExplorerTaskbar, () => $"hidden: {shell.ExplorerTaskbar.Hidden}"),
            new("service.jump-lists", jumpList, () =>
                $"explorer: {(jumpList.Available ? "available" : "unavailable")}; destinations: {jumpList.Destinations.Count}"),
            new("service.keyboard-layouts", shell.KeyboardLayouts, () =>
                $"current: {shell.KeyboardLayouts.Current?.Label ?? "none"}; layouts: {shell.KeyboardLayouts.Layouts.Count}"),
            new("service.media", shell.Media, () => $"sessions: {shell.Media.Sessions.Count}"),
            new("service.networking", shell.Networking, () =>
                $"ready: {shell.Networking.Ready}; connectivity: {shell.Networking.Connectivity}; devices: {shell.Networking.Devices.Count}"),
            new("service.hotkey", hotkey, () =>
                $"sequence: {hotkey.Sequence}; active: {hotkey.Active}; taken: {hotkey.Taken}"),
            new("service.new-idle-monitor", idle, () =>
                $"enabled: {idle.Enabled}; idle: {idle.IsIdle}; timeout: {idle.Timeout.ToString(CultureInfo.InvariantCulture)}"),
            new("service.notifications", shell.Notifications, () =>
                $"access: {shell.Notifications.AccessStatus}; notifications: {shell.Notifications.Notifications.Count}"),
            new("service.power", shell.Power, () =>
                $"on battery: {shell.Power.OnBattery}; battery: {Battery(shell.Power.DisplayDevice)}"),
            new("service.power-mode", shell.PowerMode, () =>
                $"ready: {shell.PowerMode.Ready}; mode: {shell.PowerMode.Mode}; available: {shell.PowerMode.Available}"),
            new("service.privacy", shell.Privacy, () =>
                $"microphone: {shell.Privacy.Microphone.Count}; camera: {shell.Privacy.Camera.Count}; location: {shell.Privacy.Location.Count}"),
            new("service.recent-documents", Unwatched, () => $"recent: {shell.RecentDocuments.Search("").Count}"),
            new("service.session", Unwatched, () =>
                $"sleep: {shell.Session.SleepAvailable}; hibernate: {shell.Session.HibernateAvailable}"),
            new("service.startup", shell.Startup, () =>
                $"available: {shell.Startup.Available}; run at sign-in: {shell.Startup.RunAtSignIn}"),
            new("service.system-tray", shell.SystemTray, () => $"icons: {shell.SystemTray.Items.Count}"),
            new("service.virtual-desktops", shell.VirtualDesktops, () =>
                $"supported: {shell.VirtualDesktops.Supported}; desktops: {shell.VirtualDesktops.Desktops.Count}"),
            new("service.wallpaper", shell.Wallpaper, () =>
                $"fit: {shell.Wallpaper.Fit}; screens: {shell.Wallpaper.Screens.Count}"),
            new("service.window-list", shell.WindowList, () =>
                $"windows: {shell.WindowList.Windows.Count}; active: {(shell.WindowList.ActiveWindow is null ? "none" : "yes")}"),
            new("service.winland", shell.Winland, () =>
                $"running: {shell.Winland.ProcessId > 0}; workspaces: {shell.Winland.Workspaces.Count}")
        ];
    }

    private static string Battery(PowerDevice device)
    {
        return device.IsPresent
            ? $"{Math.Round(device.Percentage * 100).ToString(CultureInfo.InvariantCulture)}%"
            : "none";
    }

    private sealed class Fixed : ObservableObject;
}
