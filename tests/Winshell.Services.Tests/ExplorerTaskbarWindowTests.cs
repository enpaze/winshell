using Winshell.ExplorerTaskbar;
using Winshell.Native;

namespace Winshell.Services.Tests;

[TestClass]
public class ExplorerTaskbarWindowTests
{
    private static readonly RECT Screen = new() { left = 0, top = 0, right = 1920, bottom = 1080 };

    [TestMethod]
    public void A_window_sits_on_the_near_edge_of_its_screen()
    {
        Assert.AreEqual(AppBarEdge.Bottom, ExplorerTaskbarWindow.EdgeOf(Bar(0, 1032, 1920, 48), Screen));
        Assert.AreEqual(AppBarEdge.Top, ExplorerTaskbarWindow.EdgeOf(Bar(0, 0, 1920, 40), Screen));
        Assert.AreEqual(AppBarEdge.Left, ExplorerTaskbarWindow.EdgeOf(Bar(0, 0, 48, 1080), Screen));
        Assert.AreEqual(AppBarEdge.Right, ExplorerTaskbarWindow.EdgeOf(Bar(1872, 0, 48, 1080), Screen));
        Assert.AreEqual(AppBarEdge.Bottom, ExplorerTaskbarWindow.EdgeOf(Screen, Screen));
    }

    private static RECT Bar(int x, int top, int width, int height)
    {
        return new RECT { left = x, top = top, right = x + width, bottom = top + height };
    }
}
