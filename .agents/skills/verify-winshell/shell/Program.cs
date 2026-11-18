using System.Globalization;

Shell.Run(shell =>
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

    var floating = new FloatingWindow<FeatureWindow> { Title = "Winshell features", Width = 460, Height = 760 };
    var panel = new PanelWindow<PanelRow>
    {
        Anchors = Edges.Top | Edges.Left,
        Width = 240,
        Height = 32,
        ReservedSize = 0,
        AboveWindows = false
    };
    var popup = new PopupWindow<PopupRow> { Width = 240, Height = 32 };
    void OpenPopup()
    {
        popup.Position = PopupPosition.At(floating.Origin);
        popup.Visible = true;
    }

    Features.Panel = Rows.Window("block.panel-window", panel);
    Features.Popup = Rows.Window("block.popup-window", popup);
    Features.Main = new FeatureWindowProps(
        [Rows.Launcher(launched), .. Rows.Services(shell, hotkey, idle, jumpList)],
        Rows.Window("block.floating-window", floating), OpenPopup, launcher.Show);
    panel.Visible = true;
    floating.Visible = true;
});
