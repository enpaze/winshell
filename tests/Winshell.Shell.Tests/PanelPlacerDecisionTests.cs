using Winshell.Native;

namespace Winshell.ShellTests;

[TestClass]
public class PanelPlacerDecisionTests
{
    private static readonly PanelMonitor PrimaryMonitor = new("primary", new RECT(0, 0, 1920, 1080),
        new RECT(0, 40, 1920, 1080), 1);

    private static readonly PanelMonitor Second =
        new("second", new RECT(2560, 1080, 5120, 2520), new RECT(2560, 1080, 5120, 2472), 1);

    private static readonly PanelMonitor Scaled =
        new("scaled", new RECT(0, 0, 1920, 1080), new RECT(0, 80, 1920, 1080), 2);

    private static readonly RECT Wide = new(1920, 0, 4480, 1440);
    private static readonly RECT WideWork = new(1920, 0, 4480, 1392);

    private sealed class Desktop : IPanelDesktop
    {
        public bool Fullscreen { get; set; }

        public int Registers { get; private set; }

        public int Removes { get; private set; }

        public int Reserves { get; private set; }

        public RECT? Asked { get; private set; }

        public RECT? Grant { get; init; }

        public int Moves { get; private set; }

        public RECT? Moved { get; private set; }

        public bool? Topmost { get; private set; }

        public double Scale { get; private set; }

        public List<Action> Posted { get; } = [];

        public List<PanelMonitor> Monitors { get; } = [Second, Scaled];

        public PanelMonitor? Find(string id)
        {
            foreach (var monitor in Monitors)
                if (monitor.Id == id)
                    return monitor;
            return null;
        }

        public PanelMonitor? Primary()
        {
            return PrimaryMonitor;
        }

        public bool FullscreenOn(string screenId)
        {
            return Fullscreen && screenId == PrimaryMonitor.Id;
        }

        public bool Register(uint callback)
        {
            Registers++;
            return true;
        }

        public RECT Query(AppBarEdge edge, RECT proposed)
        {
            return proposed;
        }

        public RECT Reserve(AppBarEdge edge, RECT desired)
        {
            Reserves++;
            Asked = desired;
            return Grant ?? desired;
        }

        public void Remove()
        {
            Removes++;
        }

        public void Move(RECT rect, bool topmost, double scale)
        {
            Moves++;
            Moved = rect;
            Topmost = topmost;
            Scale = scale;
        }

        public void WorkAreaMayHaveChanged()
        {
        }

        public void Post(Action action)
        {
            Posted.Add(action);
        }
    }

    private static PanelPlace Panel(Edges anchors, string? screen = null, bool covers = false, bool above = true,
        double reserved = 48)
    {
        return new PanelPlace(screen is null ? null : Screen(screen), anchors, 0, 0, 0, 0, 320, 48, covers,
            reserved, above);
    }

    private static ScreenInfo Screen(string id)
    {
        return new ScreenInfo(id, id, "", "", "", default, default, 1, 0, 0, 60, false);
    }

    private static PanelPlace Anchored(Edges anchors, double width = 0, double height = 0, double left = 0,
        double top = 0, double right = 0, double bottom = 0)
    {
        return new PanelPlace(null, anchors, left, top, right, bottom, width, height, false, 0, true);
    }

    private static PanelPlacer Shown(Desktop desktop)
    {
        var placer = new PanelPlacer(desktop);
        placer.Show();
        return placer;
    }

    private static RECT PlacedIn(RECT bounds, RECT area, double scale, PanelPlace panel)
    {
        var desktop = new Desktop();
        desktop.Monitors.Add(new PanelMonitor("anchored", bounds, area, scale));
        Shown(desktop).Set(panel with { Screen = Screen("anchored") });
        return desktop.Moved!.Value;
    }

    [TestMethod]
    [DataRow(Edges.Top | Edges.Left | Edges.Right, 2560, 1080, 5120, 1128)]
    [DataRow(Edges.Bottom | Edges.Left | Edges.Right, 2560, 2472, 5120, 2520)]
    [DataRow(Edges.Left | Edges.Top | Edges.Bottom, 2560, 1080, 2608, 2520)]
    [DataRow(Edges.Right | Edges.Top | Edges.Bottom, 5072, 1080, 5120, 2520)]
    [DataRow(Edges.Bottom, 2560, 2472, 5120, 2520)]
    public void A_panel_along_one_edge_asks_for_a_strip_of_that_edge_as_deep_as_its_size(Edges anchors, int left,
        int top, int right, int bottom)
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);

        placer.Set(Panel(anchors, Second.Id));

        Assert.AreEqual(1, desktop.Registers);
        Assert.AreEqual(new RECT(left, top, right, bottom), desktop.Asked);
        placer.Release();
    }

    [TestMethod]
    [DataRow(Edges.None)]
    [DataRow(Edges.Top | Edges.Left)]
    [DataRow(Edges.Left | Edges.Right)]
    [DataRow(Edges.Top | Edges.Bottom | Edges.Left | Edges.Right)]
    public void A_panel_in_a_corner_or_across_the_screen_reserves_no_edge(Edges anchors)
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);

        placer.Set(Panel(anchors));

        Assert.AreEqual(0, desktop.Registers);
        Assert.IsNull(desktop.Asked);
    }

    [TestMethod]
    [DataRow(null, Edges.Top, 0, 48, 1920, 96)]
    [DataRow(null, Edges.Bottom, 0, 984, 1920, 1032)]
    [DataRow(null, Edges.Left, 0, 48, 48, 1080)]
    [DataRow(null, Edges.Right, 1872, 0, 1920, 1032)]
    [DataRow("second", Edges.Top, 2608, 1080, 5120, 1128)]
    [DataRow("second", Edges.Bottom, 2560, 2472, 5072, 2520)]
    [DataRow("second", Edges.Left, 2560, 1080, 2608, 2472)]
    [DataRow("second", Edges.Right, 5072, 1128, 5120, 2520)]
    public void A_bar_lays_out_in_the_strip_Explorer_grants(string? screen, Edges along, int left, int top, int right,
        int bottom)
    {
        var grant = new RECT(left, top, right, bottom);
        var desktop = new Desktop { Grant = grant };
        var placer = Shown(desktop);
        var across = along is Edges.Top or Edges.Bottom ? Edges.Left | Edges.Right : Edges.Top | Edges.Bottom;

        placer.Set(new PanelPlace(screen is null ? null : Screen(screen), along | across, 0, 0, 0, 0, 48, 48, false,
            48, true));

        Assert.AreEqual(grant, desktop.Moved);
        placer.Release();
    }

    [TestMethod]
    public void A_panel_moves_at_the_scale_of_the_monitor_it_is_on()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);

        placer.Set(Panel(Edges.Top | Edges.Left, Scaled.Id));

        Assert.AreEqual(2, desktop.Scale);
        Assert.AreEqual(new RECT(0, 80, 640, 176), desktop.Moved);
    }

    [TestMethod]
    public void A_panel_that_covers_its_screen_reserves_nothing_and_fills_the_monitor()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);

        placer.Set(Panel(Edges.Left | Edges.Top | Edges.Right | Edges.Bottom, covers: true));

        Assert.IsNull(desktop.Asked);
        Assert.AreEqual(0, desktop.Registers);
        Assert.AreEqual(PrimaryMonitor.Bounds, desktop.Moved);
    }

    [TestMethod]
    public void A_corner_panel_uses_the_work_area_and_a_hidden_one_reserves_nothing()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);

        placer.Set(Panel(Edges.Top | Edges.Left));
        Assert.AreEqual(0, desktop.Registers);
        Assert.AreEqual(PrimaryMonitor.Area.top, desktop.Moved!.Value.top);

        placer.Hide();
        placer.Set(Panel(Edges.Top | Edges.Left | Edges.Right));
        Assert.AreEqual(0, desktop.Registers);
        Assert.AreEqual(PrimaryMonitor.Area.top, desktop.Moved.Value.top);
    }

    [TestMethod]
    public void A_panel_steps_behind_a_fullscreen_app_and_comes_back_when_it_leaves()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);
        var panel = Panel(Edges.Top | Edges.Left);

        placer.Set(panel);
        Assert.IsTrue(desktop.Topmost);

        desktop.Fullscreen = true;
        placer.Set(panel);
        Assert.IsFalse(desktop.Topmost);

        desktop.Fullscreen = false;
        placer.Set(panel);
        Assert.IsTrue(desktop.Topmost);

        placer.Set(Panel(Edges.Top | Edges.Left, above: false));
        Assert.IsFalse(desktop.Topmost);
    }

    [TestMethod]
    public void A_placement_that_arrives_after_the_panel_is_gone_reserves_no_edge()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);
        var panel = Panel(Edges.Top | Edges.Left | Edges.Right);

        placer.Set(panel);
        placer.OnDpiChanged(default, false);
        placer.Release();
        Assert.AreEqual(1, desktop.Removes);
        Assert.AreEqual(1, desktop.Reserves);

        desktop.Posted.Single()();

        Assert.AreEqual(1, desktop.Registers);
        Assert.AreEqual(1, desktop.Reserves);
        Assert.AreEqual(1, desktop.Moves);
    }

    [TestMethod]
    public void Explorer_restarting_drops_the_edge_and_places_the_panel_again()
    {
        var desktop = new Desktop();
        var panel = Panel(Edges.Bottom | Edges.Left | Edges.Right);
        var placer = Shown(desktop);
        placer.Set(panel);
        Assert.AreEqual(1, desktop.Registers);

        placer.OnMessage(new WindowMessage(TaskbarWindows.Created, 0, 0));

        Assert.AreEqual(1, desktop.Removes);
        Assert.AreEqual(1, desktop.Reserves);
        desktop.Posted.Single()();
        Assert.AreEqual(2, desktop.Registers);
        Assert.AreEqual(2, desktop.Reserves);
        placer.Release();
    }

    [TestMethod]
    public void A_crash_gives_back_every_edge_the_shell_holds()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);
        placer.Set(Panel(Edges.Top | Edges.Left | Edges.Right));
        Assert.AreEqual(1, desktop.Registers);

        PanelPlacer.ReleaseAll();

        Assert.AreEqual(1, desktop.Removes);
    }

    [TestMethod]
    public void A_top_bar_spans_its_area_at_the_monitor_scale()
    {
        var rect = PlacedIn(Wide, WideWork, 1.5, Anchored(Edges.Top | Edges.Left | Edges.Right, height: 32));
        Assert.AreEqual(new RECT(1920, 0, 4480, 48), rect);
    }

    [TestMethod]
    public void A_bottom_bar_follows_the_work_area_when_the_taskbar_moves()
    {
        var bar = Anchored(Edges.Bottom | Edges.Left | Edges.Right, height: 40);
        Assert.AreEqual(new RECT(1920, 1332, 4480, 1392), PlacedIn(Wide, WideWork, 1.5, bar));

        Assert.AreEqual(new RECT(1920, 1380, 4480, 1440), PlacedIn(Wide, Wide, 1.5, bar));
    }

    [TestMethod]
    public void A_corner_panel_keeps_its_margins_in_logical_pixels()
    {
        var rect = PlacedIn(Wide, WideWork, 1.5, Anchored(Edges.Bottom | Edges.Right, 200, 100, right: 8, bottom: 8));
        Assert.AreEqual(new RECT(4480 - 12 - 300, 1392 - 12 - 150, 4480 - 12, 1392 - 12), rect);
    }

    [TestMethod]
    public void An_unanchored_panel_is_centered()
    {
        var rect = PlacedIn(PrimaryMonitor.Bounds, PrimaryMonitor.Bounds, 1, Anchored(Edges.None, 400, 300));
        Assert.AreEqual(new RECT(760, 390, 1160, 690), rect);
    }

    [TestMethod]
    public void The_same_panel_is_placed_again_at_a_new_scale()
    {
        var bar = Anchored(Edges.Top | Edges.Left | Edges.Right, height: 32);
        Assert.AreEqual(32, PlacedIn(Wide, Wide, 1, bar).Height);
        Assert.AreEqual(48, PlacedIn(Wide, Wide, 1.5, bar).Height);
    }

    [TestMethod]
    public void A_panel_is_never_empty()
    {
        var rect = PlacedIn(PrimaryMonitor.Bounds, PrimaryMonitor.Bounds, 1,
            Anchored(Edges.Left | Edges.Right, height: 0, left: 1000, right: 1000));
        Assert.IsTrue(rect.Width >= 1);
        Assert.IsTrue(rect.Height >= 1);
    }
}
