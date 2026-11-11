using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

/// <summary>A widget is a Reactor component, placed by name in an area or a bar.</summary>
public sealed class ComponentWidgetTests
{
    [Fact]
    [Trait("Category", "Window")]
    public async Task A_component_widget_shows_its_controls_in_an_area_and_a_bar()
    {
        await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            Widgets.Register<LabelWidget>(name);
            using var area = new WidgetArea
            {
                Defaults = [new(name, new Dictionary<string, object> { ["text"] = "area" })]
            };
            using var window = new PanelWindow { Width = 220, Height = 90, Content = area, Visible = true };
            await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => Texts(area).Contains("area"), "the area to show the component");
            Assert.Equal("area", Assert.Single(Texts(area), text => text == "area"));
            Assert.Single(Buttons(area));

            var screen = ScreenWindowsTests.Screen("TST", "Component", "1");
            shell.Platform.Screens.Add(screen);
            try
            {
                using var bar = new Bar
                {
                    Name = "bar" + Guid.NewGuid().ToString("N"),
                    Reserve = false,
                    Center = [new(name, new Dictionary<string, object> { ["text"] = "bar" })]
                };
                var shown = await BarWindow(shell, screen);
                var content = Assert.IsAssignableFrom<DependencyObject>(shown.Content);
                await Wait.UntilAsync(() => Texts(content).Contains("bar"), "the bar to show the component");
                Assert.Single(Buttons(content));
                Assert.Contains("area", Texts(area));
            }
            finally
            {
                shell.Platform.Screens.Remove(screen);
            }

            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_changed_placement_renders_the_same_component_with_the_new_settings()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var areaName = "area" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            Widgets.Register<RememberingWidget>(widget);
            File.WriteAllText(path, $"[{areaName}]\nwidgets = [{{ widget = '{widget}', text = 'first' }}]\n");
            try
            {
                using var area = new WidgetArea { Name = areaName, Defaults = [new(widget, new Dictionary<string, object> { ["text"] = "code" })] };
                using var window = new PanelWindow { Width = 220, Height = 90, Content = area, Visible = true };
                await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);
                await Wait.UntilAsync(() => Texts(area).Contains("first|first|Horizontal"), "the component to show its first settings");

                File.WriteAllText(path, $"[{areaName}]\nwidgets = [{{ widget = '{widget}', text = 'second' }}]\n");
                await Wait.UntilAsync(() => Texts(area).Contains("first|second|Horizontal"),
                    "the same component to render the new settings");
                Assert.DoesNotContain(Texts(area), text => text.StartsWith("second|second", StringComparison.Ordinal));
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_new_orientation_or_defaults_renders_the_same_component()
    {
        await ShellFixture.On(async _ =>
        {
            var name = Guid.NewGuid().ToString();
            Widgets.Register<RememberingWidget>(name);
            using var area = new WidgetArea
            {
                Defaults = [new(name, new Dictionary<string, object> { ["text"] = "born" })]
            };
            using var window = new PanelWindow { Width = 280, Height = 120, Content = area, Visible = true };
            await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => Texts(area).Contains("born|born|Horizontal"),
                "the component to show the settings it was born with");

            area.Defaults = [new(name, new Dictionary<string, object> { ["text"] = "next" })];
            await Wait.UntilAsync(() => Texts(area).Contains("born|next|Horizontal"),
                "the same component to render the new defaults");

            area.Orientation = Orientation.Vertical;
            await Wait.UntilAsync(() => Texts(area).Contains("born|next|Vertical"),
                "the same component to render the new orientation");
            Assert.DoesNotContain(Texts(area), text => text.StartsWith("next|", StringComparison.Ordinal));
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_component_that_throws_while_rendering_shows_its_error_in_place_and_logs_it()
    {
        await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            var survivor = "survivor" + name;
            Widgets.Register<SurvivorWidget>(survivor);
            Widgets.Register<ThrowingWidget>(name);
            using var area = new WidgetArea { Name = "end", Defaults = [new(survivor), new(name)] };
            using var window = new PanelWindow { Width = 280, Height = 90, Content = area, Visible = true };
            await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => Texts(area).Any(text => text.Contains("Render failed", StringComparison.Ordinal)),
                "the throwing component to show its error");

            var text = Texts(area);
            Assert.Equal("survivor", text[0]);
            Assert.Contains(name, text[1], StringComparison.Ordinal);
            Assert.Contains("Render failed", text[1], StringComparison.Ordinal);
            var log = LogAfter(shell, $"ERROR widgets: Could not render {name}");
            Assert.Contains("Render failed", log, StringComparison.Ordinal);
            Assert.Contains("\n   at ", log, StringComparison.Ordinal);
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_component_widgets_popup_takes_the_shell_style()
    {
        await ShellFixture.On(async shell =>
        {
            shell.Style = TestStyles.Other();
            var name = Guid.NewGuid().ToString();
            Widgets.Register<PopupWidget>(name);
            try
            {
                using var area = new WidgetArea { Defaults = [new(name)] };
                using var window = new PanelWindow { Width = 180, Height = 80, Content = area, Visible = true };
                await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);
                TextBlock? popupText = null;
                await Wait.UntilAsync(() =>
                {
                    popupText = shell.Platform.Windows.Select(host => host.Client).OfType<PopupWindow>()
                        .Select(popup => FindText(popup.Content, "popup"))
                        .FirstOrDefault(text => text is not null);
                    return popupText is not null;
                }, "the popup to show its component");
                await ShellFixture.LaidOut(popupText!).WaitAsync(TestContext.Current.CancellationToken);
                Assert.Equal(("Consolas", 18d), (popupText!.FontFamily.Source, popupText.FontSize));
            }
            finally
            {
                shell.Style = null;
            }

            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_component_widgets_flyout_takes_the_shell_style()
    {
        await ShellFixture.On(async shell =>
        {
            shell.Style = TestStyles.Other();
            var name = Guid.NewGuid().ToString();
            Widgets.Register<FlyoutWidget>(name);
            try
            {
                using var area = new WidgetArea { Defaults = [new(name)] };
                using var window = new PanelWindow { Width = 180, Height = 80, Content = area, Visible = true };
                await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);
                TextBlock? flyoutText = null;
                await Wait.UntilAsync(() =>
                {
                    flyoutText = shell.Platform.Windows.Select(host => host.Client).OfType<PanelWindow>()
                        .Select(panel => panel.Content)
                        .OfType<FrameworkElement>()
                        .Where(content => content.XamlRoot is not null)
                        .SelectMany(content => VisualTreeHelper.GetOpenPopupsForXamlRoot(content.XamlRoot))
                        .Select(popup => FindText(popup.Child, "flyout"))
                        .FirstOrDefault(text => text is not null);
                    return flyoutText is not null;
                }, "the flyout to show its component");
                await ShellFixture.LaidOut(flyoutText!).WaitAsync(TestContext.Current.CancellationToken);
                Assert.Equal(("Consolas", 18d), (flyoutText!.FontFamily.Source, flyoutText.FontSize));
            }
            finally
            {
                shell.Style = null;
            }

            return true;
        });
    }

    private static string LogAfter(Shell shell, string entry)
    {
        var log = ShellFixture.ReadLog(shell);
        Assert.Contains(entry, log, StringComparison.Ordinal);
        return log[log.IndexOf(entry, StringComparison.Ordinal)..];
    }

    private static TextBlock? FindText(DependencyObject? root, string text)
    {
        if (root is null)
            return null;
        if (root is TextBlock block && block.Text == text)
            return block;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindText(VisualTreeHelper.GetChild(root, i), text);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static async Task<PanelWindow> BarWindow(Shell shell, ScreenInfo screen)
    {
        PanelWindow? window = null;
        await Wait.UntilAsync(() =>
        {
            Assert.True(shell.Screens.Contains(screen), "The platform read the screens again and dropped the made-up ones.");
            window = shell.Platform.Windows.Select(host => host.Client).OfType<PanelWindow>()
                .SingleOrDefault(panel => panel.Screen == screen);
            return window is not null;
        }, "the bar to show");
        return window!;
    }

    private static List<string> Texts(DependencyObject root)
    {
        var texts = new List<string>();
        Walk(root);
        return texts;

        void Walk(DependencyObject node)
        {
            if (node is TextBlock block)
                texts.Add(block.Text);
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
                Walk(VisualTreeHelper.GetChild(node, i));
        }
    }

    private static List<Button> Buttons(DependencyObject root)
    {
        var buttons = new List<Button>();
        Walk(root);
        return buttons;

        void Walk(DependencyObject node)
        {
            if (node is Button button)
                buttons.Add(button);
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
                Walk(VisualTreeHelper.GetChild(node, i));
        }
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_widget_reads_the_shell_window_its_area_is_in()
    {
        await ShellFixture.On(async _ =>
        {
            var name = Guid.NewGuid().ToString();
            WindowWidget.Seen = null;
            Widgets.Register<WindowWidget>(name);
            using var area = new WidgetArea { Defaults = [name] };
            using var window = new PanelWindow { Width = 160, Height = 48, Content = area, Visible = true };
            await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => WindowWidget.Seen == window, "the widget to be told its window");
            return true;
        });
    }

    /// <summary>Records the window its area is drawn in.</summary>
    private sealed class WindowWidget : Component<WidgetContext>
    {
        public static ShellWindow? Seen;

        public override Element Render()
        {
            Seen = Props.Window;
            return TextBlock("window");
        }
    }

    /// <summary>A line of text that stays while the placement beside it fails.</summary>
    private sealed class SurvivorWidget : Component<WidgetContext>
    {
        public override Element Render() => TextBlock("survivor");
    }

    /// <summary>Shows its <c>text</c> setting and a button, so a test can see the controls a placement rendered.</summary>
    private sealed class LabelWidget : Component<WidgetContext>
    {
        public override Element Render()
        {
            var text = Props.Settings.TryGetValue("text", out var value) ? (string)value : "";
            return VStack(TextBlock(text), Button("go"));
        }
    }

    /// <summary>Keeps the settings it was born with, so a new instance would show a different line.</summary>
    private sealed class RememberingWidget : Component<WidgetContext>
    {
        public override Element Render()
        {
            var text = (string)Props.Settings["text"];
            var (born, _) = UseState(text);
            return TextBlock($"{born}|{text}|{Props.Orientation}");
        }
    }

    /// <summary>Fails while rendering, which the area shows and logs.</summary>
    private sealed class ThrowingWidget : Component<WidgetContext>
    {
        public override Element Render() => throw new InvalidOperationException("Render failed");
    }

    /// <summary>Opens a popup whose content is a component styled with the shell.</summary>
    private sealed class PopupWidget : Component<WidgetContext>
    {
        public override Element Render()
        {
            UseEffect(() =>
            {
                var body = new ReactorHostControl(new PopupLabel());
                var popup = new PopupWindow
                {
                    Rect = new Rect(240, 80, 220, 80),
                    Content = Props.StylePopup(body),
                    Visible = true
                };
                return () =>
                {
                    popup.Dispose();
                    body.Dispose();
                };
            }, false);
            return TextBlock("widget");
        }
    }

    private sealed class PopupLabel : Component
    {
        public override Element Render() => TextBlock("popup");
    }

    /// <summary>Opens a flyout whose content is a component styled with the shell.</summary>
    private sealed class FlyoutWidget : Component<WidgetContext>
    {
        public override Element Render()
        {
            UseEffect(() =>
            {
                var body = new ReactorHostControl(new FlyoutLabel());
                var anchor = new TextBlock { Text = "anchor" };
                var flyout = new Flyout
                {
                    Content = Props.StylePopup(body),
                    Placement = FlyoutPlacementMode.Bottom,
                    ShouldConstrainToRootBounds = false
                };
                anchor.Loaded += (_, _) => flyout.ShowAt(anchor);
                var window = new PanelWindow
                {
                    Width = 180,
                    Height = 80,
                    Content = anchor,
                    Visible = true
                };
                return () =>
                {
                    flyout.Hide();
                    window.Dispose();
                    body.Dispose();
                };
            }, false);
            return TextBlock("widget");
        }
    }

    private sealed class FlyoutLabel : Component
    {
        public override Element Render() => TextBlock("flyout");
    }
}
