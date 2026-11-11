using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

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
            using var follows = new Mounted<Surface, SurfaceProps>(new SurfaceProps { Content = XamlHost(() => held) });
            using var own = new Mounted<Surface, SurfaceProps>(new SurfaceProps { Style = other });
            var followsHost = follows.Host;
            var ownHost = own.Host;
            using var window = new PanelWindow
            {
                Width = 100,
                Height = 40,
                Content = new StackPanel { RequestedTheme = ElementTheme.Light, Children = { followsHost, ownHost } },
                Visible = true
            };
            try
            {
                var light = WidgetAreaTests.WindowsColor("Light", "SolidBackgroundFillColorBase");
                await ShellFixture.LaidOut(followsHost).WaitAsync(TestContext.Current.CancellationToken);
                Assert.Equal(light, BarTests.BackgroundOf(followsHost));
                Assert.Equal(tokyo, BarTests.BackgroundOf(ownHost));

                shell.Style = other;
                await Wait.UntilAsync(() => BarTests.BackgroundOf(followsHost) == tokyo, "the surface to take the new style");
                Assert.Equal(0, unloaded);

                own.Set(own.Props with { Style = null });
                shell.Style = null;
                await Wait.UntilAsync(() => BarTests.BackgroundOf(followsHost) == light && BarTests.BackgroundOf(ownHost) == light,
                    "both surfaces to follow the shell back to the built-in style");

                Assert.Equal(light, BarTests.BackgroundOf(ownHost));
            }
            finally
            {
                shell.Style = null;
            }

            return true;
        });
    }
}
