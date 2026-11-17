using Winshell;

namespace OmarchyStyle;

public sealed class AppShell : ShellConfiguration
{
    public override void Start(Shell shell)
    {
        shell.ExplorerTaskbar.Hidden = true;
        shell.Style = TokyoNight.Style;
        Widgets.Register<WindowsWidget>("windows");
        Widgets.Register<WorkspacesWidget>("workspaces");
        Widgets.Register<ClockWidget>("clock");
        Widgets.Register<TrayWidget>("tray");
        Widgets.Register<BluetoothWidget>("bluetooth");
        Widgets.Register<NetworkWidget>("network");
        Widgets.Register<AudioWidget>("audio");
        Widgets.Register<ScreenWidget>("screen");
        Widgets.Register<BatteryWidget>("battery");
        _ = new Bar
        {
            Edge = Edges.Top,
            Start = ["windows", "workspaces"],
            Center = ["clock"],
            End = ["tray", "bluetooth", "network", "audio", "screen", "battery"]
        };
    }
}
