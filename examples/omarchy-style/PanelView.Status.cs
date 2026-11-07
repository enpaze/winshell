using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell.Network;
using Winshell.Power;

namespace OmarchyStyle;

public sealed partial class PanelView
{
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
        var profile = device is null
            ? null
            : watch.Read(networking.Profiles).Select(watch.Read)
                .FirstOrDefault(p => p.AdapterId == device.Id);
        var portal = networking.Connectivity == NetworkConnectivity.CaptivePortal;
        var restricted = device is not null && (portal || networking.Connectivity == NetworkConnectivity.LocalAccess);
        var name = profile?.Name ?? device?.Name;
        Show(
            Network,
            Glyphs.Network(device?.Type ?? DeviceType.None, profile?.SignalStrength ?? 0, restricted),
            NetworkToolTip(name, portal, restricted),
            restricted);
    }

    private static string NetworkToolTip(string? name, bool portal, bool restricted)
    {
        if (name is null)
            return "Disconnected";
        if (portal)
            return name + ": sign in to the network";
        if (restricted)
            return name + ": no internet";
        return name;
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

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        Process.Start(new ProcessStartInfo((string)button.Tag) { UseShellExecute = true })?.Dispose();
    }
}
