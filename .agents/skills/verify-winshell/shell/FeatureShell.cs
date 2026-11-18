using System.Globalization;
using Microsoft.UI.Reactor.Hosting;
using Windows.Foundation;

namespace FeatureMap;

public sealed class FeatureShell : ShellConfiguration
{
    public override void Start(Shell shell)
    {
        shell.Ipc.Handle("features", "echo", (string text) => text);
        shell.Ipc.Property("features", "name", () => "features");
        var ticked = shell.Ipc.Signal("features", "ticked");
        var ticks = 0;
        void Tick()
        {
            ticks++;
            ticked.Emit(ticks.ToString(CultureInfo.InvariantCulture));
            shell.Loop.Schedule(TimeSpan.FromSeconds(1), Tick);
        }

        shell.Loop.Schedule(TimeSpan.FromSeconds(1), Tick);
        Widgets.Register<FeatureWidget>("feature");

        var hotkey = shell.Hotkey("Ctrl+Alt+Shift+F13");
        var idle = shell.NewIdleMonitor();
        idle.Timeout = 600;
        idle.Enabled = true;
        var jumpList = shell.JumpLists.ForApp("Microsoft.Windows.Explorer");
        var launcher = new Launcher { Hotkey = "Ctrl+Alt+Shift+F14" };
        var launched = new Launched(shell, launcher);

        var floating = new FloatingWindow { Title = "Winshell features", Width = 460, Height = 760 };
        var panel = new PanelWindow
        {
            Anchors = Edges.Top | Edges.Left,
            Width = 240,
            Height = 32,
            ReservedSize = 0,
            AboveWindows = false
        };
        var popup = new PopupWindow();
        panel.Content = Host(() => new Row(), Rows.Window("block.panel-window", panel));
        popup.Content = Host(() => new Row(), Rows.Window("block.popup-window", popup));
        void OpenPopup()
        {
            popup.Rect = new Rect(floating.Origin.X, floating.Origin.Y, 240, 32);
            popup.Visible = true;
        }

        floating.Content = Host(() => new FeatureWindow(),
            new FeatureWindowProps(
                [Rows.Launcher(launched), .. Rows.Services(shell, hotkey, idle, jumpList)],
                Rows.Window("block.floating-window", floating), OpenPopup, launcher.Show));
        panel.Visible = true;
        floating.Visible = true;
    }

    private static ReactorHostControl Host<TProps>(Func<Component> make, TProps props)
    {
        return new ReactorHostControl { ComponentFactory = make, Props = props };
    }
}
