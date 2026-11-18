using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Winshell.ShellTests;

[Trait("Category", "Window")]
public sealed class PopupWindowTests
{
    [Theory]
    [InlineData("center")]
    [InlineData("top")]
    [InlineData("at")]
    public async Task A_popup_opens_where_its_position_says(string where)
    {
        var (origin, expected) = await ShellFixture.On(async shell =>
        {
            var screen = shell.Screens[0];
            var area = screen.WorkArea;
            var (position, expected) = where switch
            {
                "center" => (PopupPosition.Center,
                    new Point(area.X + (area.Width - 200) / 2, area.Y + (area.Height - 100) / 2)),
                "top" => (PopupPosition.Top, new Point(area.X + (area.Width - 200) / 2, area.Y + area.Height / 5)),
                _ => (PopupPosition.At(new Point(area.X + 40, area.Y + 60)), new Point(area.X + 40, area.Y + 60))
            };
            var content = new Grid();
            using var popup = new PopupWindow
            {
                Screen = screen,
                Position = position,
                Width = 200,
                Height = 100,
                Content = content,
                Visible = true
            };
            await ShellFixture.LaidOut(content);
            return (popup.Origin, expected);
        });

        Assert.InRange(origin.X, expected.X - 1, expected.X + 1);
        Assert.InRange(origin.Y, expected.Y - 1, expected.Y + 1);
    }
}
