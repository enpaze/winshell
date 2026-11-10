using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winshell.ShellTests;

/// <summary>A surface on screen, in its style's background. <see cref="BarTests"/> reads it in light and dark.</summary>
[Trait("Category", "Window")]
public sealed class SurfaceTests
{
    [Fact]
    public async Task A_surface_on_screen_follows_the_shell_style_keeping_what_it_holds_until_it_has_its_own()
    {
        await ShellFixture.On(async shell =>
        {
            var other = TestStyles.Other();
            var tokyo = global::Windows.UI.Color.FromArgb(0xff, 0x1a, 0x1b, 0x26);
            var held = new TextBlock { Text = "held" };
            var unloaded = 0;
            held.Unloaded += (_, _) => unloaded++;
            using var follows = new Surface { Content = held };
            using var own = new Surface { Style = other };
            using var window = new PanelWindow
            {
                Width = 100,
                Height = 40,
                Content = new StackPanel { RequestedTheme = ElementTheme.Light, Children = { follows, own } },
                Visible = true
            };
            try
            {
                var light = WidgetAreaTests.WindowsColor("Light", "SolidBackgroundFillColorBase");
                await ShellFixture.LaidOut(follows).WaitAsync(TestContext.Current.CancellationToken);
                Assert.Equal(light, BarTests.BackgroundOf(follows));
                Assert.Equal(tokyo, BarTests.BackgroundOf(own));

                shell.Style = other;
                await Wait.UntilAsync(() => BarTests.BackgroundOf(follows) == tokyo, "the surface to take the new style");
                Assert.Equal(0, unloaded);

                own.Style = null;
                shell.Style = null;
                await Wait.UntilAsync(() => BarTests.BackgroundOf(follows) == light && BarTests.BackgroundOf(own) == light,
                    "both surfaces to follow the shell back to the built-in style");

                Assert.Equal(light, BarTests.BackgroundOf(own));
            }
            finally
            {
                shell.Style = null;
            }

            return true;
        });
    }
}
