using Windows.Foundation;

namespace Winshell.ShellTests;

public sealed class PopupPositionTests
{
    private static readonly Rect WorkArea = new(100, 50, 1000, 800);

    [Fact]
    public void Center_puts_the_popup_in_the_middle_of_the_work_area()
    {
        Assert.Equal(new Rect(500, 400, 200, 100), PopupPosition.Center.Place(WorkArea, 200, 100));
    }

    [Fact]
    public void Top_centers_the_popup_across_the_work_area_a_fifth_of_the_way_down()
    {
        Assert.Equal(new Rect(500, 210, 200, 100), PopupPosition.Top.Place(WorkArea, 200, 100));
    }

    [Fact]
    public void At_puts_the_popups_top_left_corner_at_the_point_whatever_the_work_area()
    {
        Assert.Equal(new Rect(-300, 20, 200, 100),
            PopupPosition.At(new Point(-300, 20)).Place(WorkArea, 200, 100));
    }

    [Fact]
    public void Only_a_point_ignores_the_screen_with_the_active_app_window()
    {
        Assert.True(PopupPosition.Center.FollowsActiveWindow);
        Assert.True(PopupPosition.Top.FollowsActiveWindow);
        Assert.False(PopupPosition.At(new Point(0, 0)).FollowsActiveWindow);
    }

    [Fact]
    public void Two_positions_at_one_point_are_equal()
    {
        Assert.Equal(PopupPosition.At(new Point(4, 8)), PopupPosition.At(new Point(4, 8)));
        Assert.NotEqual(PopupPosition.At(new Point(4, 8)), PopupPosition.At(new Point(8, 4)));
        Assert.NotEqual(PopupPosition.Top, PopupPosition.Center);
    }
}
