using System.Runtime.CompilerServices;
using Winshell.Tray;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class TrayCursorTests
{
    [TestMethod]
    public void Moving_into_the_menu_keeps_the_pointer_where_the_user_left_it()
    {
        var loop = new ManualEventLoop();
        var pointer = new StrongBox<(int X, int Y)>((100, 100));
        using var cursor = new TrayCursor(loop, () => pointer.Value, point => pointer.Value = point);

        cursor.Place((500, 500));
        Assert.AreEqual((500, 500), pointer.Value);
        pointer.Value = (520, 540);
        loop.Advance(TimeSpan.FromMilliseconds(400));

        Assert.AreEqual((520, 540), pointer.Value);
    }

    [TestMethod]
    public void A_keyboard_menu_restores_an_untouched_pointer()
    {
        var loop = new ManualEventLoop();
        (int X, int Y) pointer = (100, 100);
        using var cursor = new TrayCursor(loop, () => pointer, point => pointer = point);

        cursor.Place((500, 500));
        loop.Advance(TimeSpan.FromMilliseconds(399));
        Assert.AreEqual((500, 500), pointer);
        loop.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual((100, 100), pointer);
    }

    [TestMethod]
    public void A_mouse_click_near_the_pointer_needs_no_move_or_timer()
    {
        var loop = new ManualEventLoop();
        using var cursor = new TrayCursor(loop, () => (100, 100), _ => Assert.Fail("The pointer moved."));

        cursor.Place((102, 99));

        Assert.IsNull(loop.NextDue);
    }

    [TestMethod]
    public void Disposing_the_tray_cancels_a_pending_restore()
    {
        var loop = new ManualEventLoop();
        (int X, int Y) pointer = (100, 100);
        var cursor = new TrayCursor(loop, () => pointer, point => pointer = point);
        cursor.Place((500, 500));

        cursor.Dispose();
        loop.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual((500, 500), pointer);
    }
}
