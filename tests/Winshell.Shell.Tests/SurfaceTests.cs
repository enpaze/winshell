using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

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
                shell.Style = new();
                await Wait.UntilAsync(() => BarTests.BackgroundOf(followsHost) == light && BarTests.BackgroundOf(ownHost) == light,
                    "both surfaces to follow the shell back to the default style");

                Assert.Equal(light, BarTests.BackgroundOf(ownHost));
                var panel = (StackPanel)window.Content;
                panel.RequestedTheme = ElementTheme.Dark;
                var dark = WidgetAreaTests.WindowsColor("Default", "SolidBackgroundFillColorBase");
                await Wait.UntilAsync(
                    () => BarTests.BackgroundOf(followsHost) == dark && BarTests.BackgroundOf(ownHost) == dark,
                    "both surfaces to follow a theme switch after returning to the default style");
            }
            finally
            {
                shell.Style = new();
            }

            return true;
        });
    }

    [Fact]
    public async Task A_surface_whose_style_sets_only_the_font_size_paints_windows_background_in_light_and_dark()
    {
        await ShellFixture.On(async _ =>
        {
            using var surface = new Mounted<Surface, SurfaceProps>(new SurfaceProps { Style = new ShellStyle { FontSize = 12 } });
            var host = surface.Host;
            using var window = new PanelWindow { Width = 100, Height = 40, Content = host, Visible = true };
            await ShellFixture.LaidOut(host).WaitAsync(TestContext.Current.CancellationToken);
            foreach (var (requested, theme) in new[] { (ElementTheme.Light, "Light"), (ElementTheme.Dark, "Default") })
            {
                host.RequestedTheme = requested;
                var windows = WidgetAreaTests.WindowsColor(theme, "SolidBackgroundFillColorBase");
                await Wait.UntilAsync(() => BarTests.BackgroundOf(host) == windows, "the surface to paint Windows' background");
            }

            return true;
        });
    }

    [Fact]
    public async Task An_alert_in_a_surface_takes_each_new_style_without_a_theme_switch()
    {
        await ShellFixture.On(async shell =>
        {
            var text = new TextBlock { Text = "alert" };
            using var surface = new Mounted<Surface, SurfaceProps>(new SurfaceProps
            {
                Content = XamlHost(() => text).Alert(),
                Style = TestStyles.Other()
            });
            var host = surface.Host;
            host.RequestedTheme = ElementTheme.Light;
            using var window = new PanelWindow { Width = 100, Height = 40, Content = host, Visible = true };
            var contrast = shell.Platform.IsHighContrast;
            var highlight = WidgetAreaTests.WindowsColor("HighContrast", "SystemColorHighlightColor");
            var critical = WidgetAreaTests.WindowsColor("Light", "SystemFillColorCritical");
            async Task Shows(global::Windows.UI.Color? alert, double size, string what)
            {
                var color = contrast ? highlight : alert ?? critical;
                await Wait.UntilAsync(
                    () => text.Foreground is SolidColorBrush brush && brush.Color == color && text.FontSize == size, what);
            }

            try
            {
                await Shows(Microsoft.UI.Colors.Orange, 18, "the alert to take the surface style's alert color");

                surface.Set(surface.Props with { Style = new ShellStyle { FontSize = 12 } });
                await Shows(null, 12, "the alert to take Windows' critical color under a font-only style");

                surface.Set(surface.Props with { Style = TestStyles.Other() });
                await Shows(Microsoft.UI.Colors.Orange, 18, "the alert to take the new style's alert color");

                surface.Set(surface.Props with { Style = null });
                await Shows(null, 14, "the alert to follow the shell's default style");

                shell.Style = TestStyles.Other();
                await Shows(Microsoft.UI.Colors.Orange, 18, "the alert to follow the shell style's alert color");

                shell.Style = new ShellStyle { FontSize = 12 };
                await Shows(null, 12, "the alert to follow a font-only shell style");
            }
            finally
            {
                shell.Style = new();
            }

            return true;
        });
    }
}
