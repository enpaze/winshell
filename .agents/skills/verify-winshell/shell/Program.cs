using System.Globalization;

Shell.Run(shell =>
{
    shell.Ipc.Handle("features", "echo", (string text) => text);
    shell.Ipc.Property("features", "name", () => "features");
    var ticked = shell.Ipc.Signal("features", "ticked");
    _ = Tick();
    async Task Tick()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        for (var ticks = 1; await timer.WaitForNextTickAsync(); ticks++)
            ticked.Emit(ticks.ToString(CultureInfo.InvariantCulture));
    }

    var hotkey = shell.Hotkey("Ctrl+Alt+Shift+F13");
    var idle = shell.NewIdleMonitor();
    idle.Timeout = 600;
    idle.Enabled = true;
    var jumpList = shell.JumpLists.ForApp("Microsoft.Windows.Explorer");
    var launcher = shell.Add(new Launcher { Hotkey = "Ctrl+Alt+Shift+F14" });
    var launched = new Launched(shell, launcher);

    FloatingWindow<FeatureWindow, FeatureWindowProps> floating = null!;
    PanelWindow<PanelRow, WindowRowProps> panel = null!;
    PopupWindow<PopupRow, WindowRowProps> popup = null!;
    popup = shell.Add(new PopupWindow<PopupRow, WindowRowProps>(new WindowRowProps("block.popup-window", () => popup))
    {
        Width = 240,
        Height = 32
    });
    panel = shell.Add(new PanelWindow<PanelRow, WindowRowProps>(new WindowRowProps("block.panel-window", () => panel))
    {
        Edge = Edge.Top,
        Align = PanelAlign.Start,
        Width = 240,
        Height = 32,
        AboveWindows = false
    });
    floating = shell.Add(new FloatingWindow<FeatureWindow, FeatureWindowProps>(new FeatureWindowProps(
        [Rows.Launcher(launched), .. Rows.Services(shell, hotkey, idle, jumpList)],
        new WindowRowProps("block.floating-window", () => floating), OpenPopup, launcher.Show))
    {
        Title = "Winshell features",
        Width = 460,
        Height = 760
    });
    void OpenPopup()
    {
        popup.Position = PopupPosition.At(floating.Origin);
        popup.Visible = true;
    }
});
