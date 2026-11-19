using Winshell.Native;

namespace Winshell.ShellTests;

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

    private static PanelPlace Panel(Edge edge, PanelAlign align = PanelAlign.Stretch, string? screen = null,
        bool covers = false, bool above = true, bool reserve = false, double width = 320, double height = 48)
    {
        return new PanelPlace(screen is null ? null : Screen(screen), edge, align, 0, 0, 0, 0, width, height, covers,
            reserve, above);
    }

    private static ScreenInfo Screen(string id)
    {
        return new ScreenInfo(id, id, "", "", "", default, default, 1, 0, 0, 60, false);
    }

    private static PanelPlace Anchored(Edge edge, PanelAlign align, double width = 0, double height = 0,
        double left = 0, double top = 0, double right = 0, double bottom = 0)
    {
        return new PanelPlace(null, edge, align, left, top, right, bottom, width, height, false, false, true);
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

    [Fact]
    public async Task A_new_panel_sits_at_the_center_of_the_top_edge_and_reserves_nothing()
    {
        var defaults = await ShellFixture.On(_ =>
        {
            using var panel = new PanelWindow();
            return (panel.Edge, panel.Align, panel.Reserve, panel.CoversScreen);
        });

        Assert.Equal((Edge.Top, PanelAlign.Center, false, false), defaults);
    }

    [Theory]
    [InlineData(Edge.Top, PanelAlign.Stretch, 2560, 1080, 5120, 1128)]
    [InlineData(Edge.Bottom, PanelAlign.Stretch, 2560, 2472, 5120, 2520)]
    [InlineData(Edge.Left, PanelAlign.Stretch, 2560, 1080, 2624, 2520)]
    [InlineData(Edge.Right, PanelAlign.Stretch, 5056, 1080, 5120, 2520)]
    [InlineData(Edge.Bottom, PanelAlign.Center, 2560, 2472, 5120, 2520)]
    [InlineData(Edge.Top, PanelAlign.Start, 2560, 1080, 5120, 1128)]
    [InlineData(Edge.Right, PanelAlign.End, 5056, 1080, 5120, 2520)]
    public void A_reserving_panel_asks_for_a_strip_of_its_edge_as_deep_as_its_thickness(Edge edge, PanelAlign align,
        int left, int top, int right, int bottom)
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);

        placer.Set(Panel(edge, align, Second.Id, reserve: true, width: 64, height: 48));

        Assert.Equal(1, desktop.Registers);
        Assert.Equal(new RECT(left, top, right, bottom), desktop.Asked);
        placer.Release();
    }

    [Theory]
    [InlineData(Edge.Top, PanelAlign.Stretch, false, 48)]
    [InlineData(Edge.Left, PanelAlign.Start, false, 48)]
    [InlineData(Edge.Bottom, PanelAlign.Center, false, 48)]
    [InlineData(Edge.Right, PanelAlign.End, false, 48)]
    [InlineData(Edge.Top, PanelAlign.Stretch, true, 0)]
    public void A_panel_that_does_not_reserve_or_has_no_thickness_reserves_no_edge(Edge edge, PanelAlign align,
        bool reserve, double thickness)
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);

        placer.Set(Panel(edge, align, reserve: reserve, width: thickness, height: thickness));

        Assert.Equal(0, desktop.Registers);
        Assert.Null(desktop.Asked);
    }

    [Theory]
    [InlineData(null, Edge.Top, 0, 48, 1920, 96)]
    [InlineData(null, Edge.Bottom, 0, 984, 1920, 1032)]
    [InlineData(null, Edge.Left, 0, 48, 48, 1080)]
    [InlineData(null, Edge.Right, 1872, 0, 1920, 1032)]
    [InlineData("second", Edge.Top, 2608, 1080, 5120, 1128)]
    [InlineData("second", Edge.Bottom, 2560, 2472, 5072, 2520)]
    [InlineData("second", Edge.Left, 2560, 1080, 2608, 2472)]
    [InlineData("second", Edge.Right, 5072, 1128, 5120, 2520)]
    public void A_bar_lays_out_in_the_strip_Explorer_grants(string? screen, Edge edge, int left, int top, int right,
        int bottom)
    {
        var grant = new RECT(left, top, right, bottom);
        var desktop = new Desktop { Grant = grant };
        var placer = Shown(desktop);

        placer.Set(Panel(edge, screen: screen, reserve: true, width: 48, height: 48));

        Assert.Equal(grant, desktop.Moved);
        placer.Release();
    }

    [Fact]
    public void A_panel_moves_at_the_scale_of_the_monitor_it_is_on()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);

        placer.Set(Panel(Edge.Top, PanelAlign.Start, Scaled.Id));

        Assert.Equal(2, desktop.Scale);
        Assert.Equal(new RECT(0, 80, 640, 176), desktop.Moved);
    }

    [Fact]
    public void A_panel_that_covers_its_screen_reserves_nothing_and_fills_the_monitor()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);

        placer.Set(Panel(Edge.Top, PanelAlign.Start, covers: true, reserve: true));

        Assert.Null(desktop.Asked);
        Assert.Equal(0, desktop.Registers);
        Assert.Equal(PrimaryMonitor.Bounds, desktop.Moved);
    }

    [Fact]
    public void A_panel_that_does_not_reserve_uses_the_work_area_and_a_hidden_one_reserves_nothing()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);

        placer.Set(Panel(Edge.Top, PanelAlign.Start));
        Assert.Equal(0, desktop.Registers);
        Assert.Equal(PrimaryMonitor.Area.top, desktop.Moved!.Value.top);

        placer.Hide();
        placer.Set(Panel(Edge.Top, reserve: true));
        Assert.Equal(0, desktop.Registers);
        Assert.Equal(PrimaryMonitor.Area.top, desktop.Moved.Value.top);
    }

    [Fact]
    public void A_panel_steps_behind_a_fullscreen_app_and_comes_back_when_it_leaves()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);
        var panel = Panel(Edge.Top, PanelAlign.Start);

        placer.Set(panel);
        Assert.True(desktop.Topmost);

        desktop.Fullscreen = true;
        placer.Set(panel);
        Assert.False(desktop.Topmost);

        desktop.Fullscreen = false;
        placer.Set(panel);
        Assert.True(desktop.Topmost);

        placer.Set(Panel(Edge.Top, PanelAlign.Start, above: false));
        Assert.False(desktop.Topmost);
    }

    [Fact]
    public void A_placement_that_arrives_after_the_panel_is_gone_reserves_no_edge()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);
        var panel = Panel(Edge.Top, reserve: true);

        placer.Set(panel);
        placer.OnDpiChanged(default, false);
        placer.Release();
        Assert.Equal(1, desktop.Removes);
        Assert.Equal(1, desktop.Reserves);

        desktop.Posted.Single()();

        Assert.Equal(1, desktop.Registers);
        Assert.Equal(1, desktop.Reserves);
        Assert.Equal(1, desktop.Moves);
    }

    [Fact]
    public void Explorer_restarting_drops_the_edge_and_places_the_panel_again()
    {
        var desktop = new Desktop();
        var panel = Panel(Edge.Bottom, reserve: true);
        var placer = Shown(desktop);
        placer.Set(panel);
        Assert.Equal(1, desktop.Registers);

        placer.OnMessage(new WindowMessage(TaskbarWindows.Created, 0, 0));

        Assert.Equal(1, desktop.Removes);
        Assert.Equal(1, desktop.Reserves);
        desktop.Posted.Single()();
        Assert.Equal(2, desktop.Registers);
        Assert.Equal(2, desktop.Reserves);
        placer.Release();
    }

    [Fact]
    public void A_crash_gives_back_every_edge_the_shell_holds()
    {
        var desktop = new Desktop();
        var placer = Shown(desktop);
        placer.Set(Panel(Edge.Top, reserve: true));
        Assert.Equal(1, desktop.Registers);

        PanelPlacer.ReleaseAll();

        Assert.Equal(1, desktop.Removes);
    }

    [Fact]
    public void A_top_bar_spans_its_area_at_the_monitor_scale()
    {
        var rect = PlacedIn(Wide, WideWork, 1.5, Anchored(Edge.Top, PanelAlign.Stretch, height: 32));
        Assert.Equal(new RECT(1920, 0, 4480, 48), rect);
    }

    [Fact]
    public void A_bottom_bar_follows_the_work_area_when_the_taskbar_moves()
    {
        var bar = Anchored(Edge.Bottom, PanelAlign.Stretch, height: 40);
        Assert.Equal(new RECT(1920, 1332, 4480, 1392), PlacedIn(Wide, WideWork, 1.5, bar));

        Assert.Equal(new RECT(1920, 1380, 4480, 1440), PlacedIn(Wide, Wide, 1.5, bar));
    }

    [Fact]
    public void A_corner_panel_keeps_its_margins_in_logical_pixels()
    {
        var rect = PlacedIn(Wide, WideWork, 1.5,
            Anchored(Edge.Bottom, PanelAlign.End, 200, 100, right: 8, bottom: 8));
        Assert.Equal(new RECT(4480 - 12 - 300, 1392 - 12 - 150, 4480 - 12, 1392 - 12), rect);
    }

    [Theory]
    [InlineData(Edge.Top, PanelAlign.Stretch, 0, 0, 1920, 300)]
    [InlineData(Edge.Top, PanelAlign.Start, 0, 0, 400, 300)]
    [InlineData(Edge.Top, PanelAlign.Center, 760, 0, 1160, 300)]
    [InlineData(Edge.Top, PanelAlign.End, 1520, 0, 1920, 300)]
    [InlineData(Edge.Bottom, PanelAlign.Stretch, 0, 780, 1920, 1080)]
    [InlineData(Edge.Bottom, PanelAlign.Start, 0, 780, 400, 1080)]
    [InlineData(Edge.Bottom, PanelAlign.Center, 760, 780, 1160, 1080)]
    [InlineData(Edge.Bottom, PanelAlign.End, 1520, 780, 1920, 1080)]
    [InlineData(Edge.Left, PanelAlign.Stretch, 0, 0, 400, 1080)]
    [InlineData(Edge.Left, PanelAlign.Start, 0, 0, 400, 300)]
    [InlineData(Edge.Left, PanelAlign.Center, 0, 390, 400, 690)]
    [InlineData(Edge.Left, PanelAlign.End, 0, 780, 400, 1080)]
    [InlineData(Edge.Right, PanelAlign.Stretch, 1520, 0, 1920, 1080)]
    [InlineData(Edge.Right, PanelAlign.Start, 1520, 0, 1920, 300)]
    [InlineData(Edge.Right, PanelAlign.Center, 1520, 390, 1920, 690)]
    [InlineData(Edge.Right, PanelAlign.End, 1520, 780, 1920, 1080)]
    public void A_panel_stretches_along_its_edge_or_sits_at_its_start_center_or_end(Edge edge, PanelAlign align,
        int left, int top, int right, int bottom)
    {
        var rect = PlacedIn(PrimaryMonitor.Bounds, PrimaryMonitor.Bounds, 1, Anchored(edge, align, 400, 300));
        Assert.Equal(new RECT(left, top, right, bottom), rect);
    }

    [Fact]
    public void The_same_panel_is_placed_again_at_a_new_scale()
    {
        var bar = Anchored(Edge.Top, PanelAlign.Stretch, height: 32);
        Assert.Equal(32, PlacedIn(Wide, Wide, 1, bar).Height);
        Assert.Equal(48, PlacedIn(Wide, Wide, 1.5, bar).Height);
    }

    [Fact]
    public void A_panel_is_never_empty()
    {
        var rect = PlacedIn(PrimaryMonitor.Bounds, PrimaryMonitor.Bounds, 1,
            Anchored(Edge.Top, PanelAlign.Stretch, height: 0, left: 1000, right: 1000));
        Assert.True(rect.Width >= 1);
        Assert.True(rect.Height >= 1);
    }
}
