using Winshell.Native;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class WindowOnScreenTests
{
    private static readonly RECT PrimaryMonitor = new(0, 0, 1920, 1080);
    private static readonly RECT PrimaryWork = new(0, 48, 1920, 1080);
    private static readonly MonitorFrame Primary = Monitor(PrimaryMonitor, PrimaryWork, 1).Frame;

    private static readonly MonitorFrame Scaled =
        Monitor(new RECT(1920, 0, 4480, 1440), new RECT(1920, 0, 4480, 1440), 1.5).Frame;

    [TestMethod]
    public void A_window_keeps_its_logical_size_and_work_area_offset_across_scales()
    {
        var shown = new RECT(100, 148, 500, 448);
        var restored = new RECT(100, 100, 500, 400);
        var window = new WindowToMove(shown, restored, true, false, false, false);

        Assert.IsTrue(WindowOnScreen.TryMove(window, Primary, Scaled, PrimaryMonitor, PrimaryWork, out var place));
        Assert.AreEqual(new RECT(2070, 150, 2670, 600), place.Screen);
        Assert.AreEqual(new RECT(2070, 102, 2670, 552), place.Workspace);

        var back = new WindowToMove(place.Screen, place.Workspace, true, false, false, false);
        Assert.IsTrue(WindowOnScreen.TryMove(back, Scaled, Primary, PrimaryMonitor, PrimaryWork, out var returned));
        Assert.AreEqual(shown, returned.Screen);
        Assert.AreEqual(restored, returned.Workspace);
    }

    [TestMethod]
    public void A_maximized_or_minimized_window_moves_by_its_restored_rectangle()
    {
        var restored = new RECT(100, 100, 500, 400);
        var maximized = new WindowToMove(new RECT(0, 0, 1920, 1080), restored, true, false, true, false);
        var minimized = new WindowToMove(new RECT(-32000, -32000, -31800, -31900), restored, true, true, false, false);

        Assert.IsTrue(WindowOnScreen.TryMove(maximized, Primary, Scaled, PrimaryMonitor, PrimaryWork, out var zoomed));
        Assert.IsTrue(WindowOnScreen.TryMove(minimized, Primary, Scaled, PrimaryMonitor, PrimaryWork, out var iconic));
        Assert.AreEqual(new RECT(2070, 150, 2670, 600), zoomed.Screen);
        Assert.AreEqual(zoomed, iconic);

        var tool = maximized with { ToolWindow = true };
        Assert.IsTrue(WindowOnScreen.TryMove(tool, Primary, Scaled, PrimaryMonitor, PrimaryWork, out var toolPlace));
        Assert.AreEqual(new RECT(2070, 78, 2670, 528), toolPlace.Screen);
        Assert.AreEqual(toolPlace.Screen, toolPlace.Workspace);
    }

    [TestMethod]
    public void A_restored_window_moves_by_the_rectangle_it_shows()
    {
        var shown = new RECT(130, 160, 530, 460);
        var restored = new RECT(100, 100, 500, 400);
        var window = new WindowToMove(shown, restored, true, false, false, false);

        Assert.IsTrue(WindowOnScreen.TryMove(window, Primary, Scaled, PrimaryMonitor, PrimaryWork, out var place));
        Assert.AreEqual(new RECT(2115, 168, 2715, 618), place.Screen);
        Assert.AreEqual(new RECT(2085, 108, 2685, 558), place.Workspace);
    }

    [TestMethod]
    public void A_window_that_would_hang_off_the_target_is_slid_back()
    {
        var from = new MonitorFrame(0, 0, 1920, 1080, 1);
        var narrow = new MonitorFrame(1920, 0, 2920, 1080, 1);
        var window = new WindowToMove(new RECT(1800, 0, 2200, 100), default, false, false, false, false);

        Assert.IsTrue(WindowOnScreen.TryMove(window, from, narrow, default, default, out var place));
        Assert.AreEqual(new RECT(2520, 0, 2920, 100), place.Screen);
    }

    [TestMethod]
    public void A_window_larger_than_the_target_shrinks_to_fit()
    {
        var window = new WindowToMove(new RECT(0, 0, 3000, 2000), default, false, false, false, false);

        Assert.IsTrue(WindowOnScreen.TryMove(window, new MonitorFrame(0, 0, 4000, 3000, 1),
            new MonitorFrame(0, 0, 800, 600, 1), default, default, out var place));
        Assert.AreEqual(new RECT(0, 0, 800, 600), place.Screen);
    }

    [TestMethod]
    public void A_window_above_the_work_area_starts_at_the_target_corner()
    {
        var window = new WindowToMove(new RECT(-50, -20, 150, 80), default, false, false, false, false);

        Assert.IsTrue(WindowOnScreen.TryMove(window, new MonitorFrame(0, 0, 1920, 1080, 1),
            new MonitorFrame(1920, 0, 3840, 1080, 1), default, default, out var place));
        Assert.AreEqual(new RECT(1920, 0, 2120, 100), place.Screen);
    }

    [TestMethod]
    public void A_bad_monitor_frame_leaves_the_window_where_it_is()
    {
        var shown = new RECT(10, 10, 20, 20);
        var window = new WindowToMove(shown, default, false, false, false, false);

        Assert.IsTrue(WindowOnScreen.TryMove(window, Primary with { Scale = 0 }, Scaled, PrimaryMonitor, PrimaryWork,
            out var place));
        Assert.AreEqual(shown, place.Screen);
    }

    [TestMethod]
    public void A_window_with_no_rectangle_is_not_moved()
    {
        var window = new WindowToMove(default, default, false, false, false, false);

        Assert.IsFalse(WindowOnScreen.TryMove(window, Primary, Scaled, PrimaryMonitor, PrimaryWork, out _));
    }

    [TestMethod]
    public void The_restored_rectangle_comes_back_in_screen_pixels_without_being_slid_onto_the_monitor()
    {
        var shown = new RECT(0, 0, 1920, 1080);
        var restored = new RECT(-50, -20, 150, 80);
        var maximized = new WindowToMove(shown, restored, true, false, true, false);

        Assert.AreEqual(new RECT(-50, 28, 150, 128), WindowOnScreen.Restored(maximized, PrimaryMonitor, PrimaryWork));
        Assert.AreEqual(restored,
            WindowOnScreen.Restored(maximized with { ToolWindow = true }, PrimaryMonitor, PrimaryWork));
        Assert.AreEqual(shown,
            WindowOnScreen.Restored(maximized with { Restored = default }, PrimaryMonitor, PrimaryWork));
        Assert.AreEqual(new RECT(14, 20, 214, 120),
            WindowOnScreen.Restored(maximized, new RECT(-3, 0, 1917, 1080), new RECT(61, 40, 1917, 1080)));
        Assert.AreEqual(restored, WindowOnScreen.Restored(maximized, PrimaryMonitor, default));
    }

    [TestMethod]
    public void The_frame_a_window_is_placed_in_is_the_work_area()
    {
        var bounds = new RECT(0, 0, 1920, 1080);
        Assert.AreEqual(new MonitorFrame(0, 48, 1920, 1080, 1), Monitor(bounds, new RECT(0, 48, 1920, 1080), 1).Frame);
        Assert.AreEqual(new MonitorFrame(0, 0, 1920, 1080, 1.5), Monitor(bounds, default, 1.5).Frame);
        Assert.AreEqual(new MonitorFrame(0, 0, 1920, 1080, 1), Monitor(bounds, new RECT(0, 48, 1920, 48), 1).Frame);
    }

    private static MonitorGeometry Monitor(RECT bounds, RECT work, double scale)
    {
        return new MonitorGeometry(0, "", bounds, work, scale, false);
    }
}
