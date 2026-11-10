using Winshell;

namespace OmarchyStyle;

/// <summary>Omarchy's bar on one edge of every screen, in its Tokyo Night style.</summary>
public sealed class AppShell : ShellConfiguration
{
    public override void Start(Shell shell)
    {
        // Winshell shows Explorer's taskbar again when the shell quits, however it quits.
        shell.ExplorerTaskbar.Hidden = true;
        shell.Style = TokyoNight.Style;
        // A component is registered in code. The [bar] section of the preferences file can move the bar and change
        // these lists, for every screen or, in a [[bar.screen]] row, for one.
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
