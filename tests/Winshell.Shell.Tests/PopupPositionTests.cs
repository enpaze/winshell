using Windows.Foundation;

namespace Winshell.ShellTests;

[TestClass]
public sealed class PopupPositionTests
{
    private static readonly Rect WorkArea = new(100, 50, 1000, 800);

    [TestMethod]
    public void Center_puts_the_popup_in_the_middle_of_the_work_area()
    {
        Assert.AreEqual(new Rect(500, 400, 200, 100), PopupPosition.Center.Place(WorkArea, 200, 100));
    }

    [TestMethod]
    public void Top_centers_the_popup_across_the_work_area_a_fifth_of_the_way_down()
    {
        Assert.AreEqual(new Rect(500, 210, 200, 100), PopupPosition.Top.Place(WorkArea, 200, 100));
    }

    [TestMethod]
    public void At_puts_the_popups_top_left_corner_at_the_point_whatever_the_work_area()
    {
        Assert.AreEqual(new Rect(-300, 20, 200, 100),
            PopupPosition.At(new Point(-300, 20)).Place(WorkArea, 200, 100));
    }

    [TestMethod]
    public void Only_a_point_ignores_the_screen_with_the_active_app_window()
    {
        Assert.IsTrue(PopupPosition.Center.FollowsActiveWindow);
        Assert.IsTrue(PopupPosition.Top.FollowsActiveWindow);
        Assert.IsFalse(PopupPosition.At(new Point(0, 0)).FollowsActiveWindow);
    }

    [TestMethod]
    public void Two_positions_at_one_point_are_equal()
    {
        Assert.AreEqual(PopupPosition.At(new Point(4, 8)), PopupPosition.At(new Point(4, 8)));
        Assert.AreNotEqual(PopupPosition.At(new Point(4, 8)), PopupPosition.At(new Point(8, 4)));
        Assert.AreNotEqual(PopupPosition.Top, PopupPosition.Center);
    }
}
