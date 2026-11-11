using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

/// <summary>
/// A bar on every screen, from its properties, its section of the preferences file and each screen's row. The screens
/// are made up and added to the shell's own list, as <see cref="ScreenWindowsTests"/> does. A panel on a screen
/// Windows doesn't have is placed on the primary screen, so no bar here reserves its edge: that would move the real
/// work area, and the platform would read the screens again, at a time no test can wait for, and drop the made-up ones
/// of whichever test runs then.
/// </summary>
[Trait("Category", "Window")]
public sealed class BarTests
{
    [Fact]
    public async Task A_bar_shows_on_each_screen_and_follows_a_screen_that_comes_or_goes()
    {
        await ShellFixture.On(async shell =>
        {
            var screens = shell.Platform.Screens;
            var first = ScreenWindowsTests.Screen("TST", "First", "1");
            var second = ScreenWindowsTests.Screen("TST", "Second", "2");
            var widget = Widget(out var made);
            screens.Add(first);
            try
            {
                using var bar = new Bar { Name = "bar" + Guid.NewGuid().ToString("N"), Reserve = false, Center = [widget] };
                await NextTurn(shell);
                var firstBar = Assert.Single(BarsOn(shell, first));
                Assert.Equal(Edges.Left | Edges.Right | Edges.Top, firstBar.Anchors);
                Assert.Equal(32, firstBar.Height);
                Assert.All(await AreasOf(firstBar), area => Assert.Equal(Orientation.Horizontal, area.Orientation));
                await Wait.UntilAsync(() => TextsOn(made, firstBar).SequenceEqual([""]), "the center widget to render");

                screens.Add(second);
                Assert.Single(BarsOn(shell, second));
                screens.Remove(second);
                Assert.Empty(BarsOn(shell, second));
                Assert.Same(firstBar, Assert.Single(BarsOn(shell, first)));

                bar.Dispose();
                Assert.Empty(BarsOn(shell, first));
            }
            finally
            {
                screens.Remove(first);
                screens.Remove(second);
            }

            return true;
        });
    }

    [Fact]
    public async Task Changing_the_edge_a_list_or_a_screen_row_changes_that_bar_live_and_a_side_bar_runs_its_areas_down()
    {
        await ShellFixture.On(async shell =>
        {
            var name = "bar" + Guid.NewGuid().ToString("N");
            var wide = ScreenWindowsTests.Screen("TST", "Wide", "1");
            var tall = ScreenWindowsTests.Screen("TST", "Tall", "1");
            var widget = Widget(out var made);
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Section(name, widget, "top", "left", "from the file"));
            shell.Platform.Screens.Add(wide);
            shell.Platform.Screens.Add(tall);
            try
            {
                using var bar = new Bar
                {
                    Name = name,
                    Reserve = false,
                    Start = [new(widget, new Dictionary<string, object> { ["text"] = "from the code" })]
                };
                await NextTurn(shell);
                var wideBar = Assert.Single(BarsOn(shell, wide));
                Assert.Equal(Edges.Left | Edges.Right | Edges.Top, wideBar.Anchors);
                Assert.Equal(0, wideBar.ReservedSize);
                await Wait.UntilAsync(() => TextsOn(made, wideBar).SequenceEqual(["from the file"]),
                    "the file's text to render");
                var tallBar = Assert.Single(BarsOn(shell, tall));
                Assert.Equal(Edges.Left | Edges.Top | Edges.Bottom, tallBar.Anchors);
                Assert.Equal(32, tallBar.Width);
                Assert.All(await AreasOf(tallBar), area => Assert.Equal(Orientation.Vertical, area.Orientation));

                await Save(shell, path, Section(name, widget, "top", "right", "from the file"));
                tallBar = await Replaced(shell, tall, tallBar);
                Assert.Equal(Edges.Right | Edges.Top | Edges.Bottom, tallBar.Anchors);
                Assert.Same(wideBar, Assert.Single(BarsOn(shell, wide)));

                await Save(shell, path, Section(name, widget, "top", "middle", "from the file"));
                tallBar = await Replaced(shell, tall, tallBar);
                Assert.Equal(Edges.Left | Edges.Right | Edges.Top, tallBar.Anchors);
                Assert.Contains("edge in its [[" + name + ".screen]] row must be", ShellFixture.ReadLog(shell),
                    StringComparison.Ordinal);

                await Save(shell, path, Section(name, widget, "bottom", "right", "saved"));
                wideBar = await Replaced(shell, wide, wideBar);
                tallBar = await Replaced(shell, tall, tallBar);
                Assert.Equal(Edges.Left | Edges.Right | Edges.Bottom, wideBar.Anchors);
                Assert.All(await AreasOf(wideBar), area => Assert.Equal(Orientation.Horizontal, area.Orientation));
                Assert.Equal(Edges.Right | Edges.Top | Edges.Bottom, tallBar.Anchors);
                await Wait.UntilAsync(
                    () => TextsOn(made, wideBar).SequenceEqual(["saved"]) && TextsOn(made, tallBar).SequenceEqual(["saved"]),
                    "the saved text to render");
            }
            finally
            {
                shell.Platform.Screens.Remove(wide);
                shell.Platform.Screens.Remove(tall);
                File.Delete(path);
            }

            return true;
        });
    }

    [Fact]
    public async Task Two_bars_with_different_names_keep_separate_sections()
    {
        await ShellFixture.On(async shell =>
        {
            var top = "top" + Guid.NewGuid().ToString("N");
            var bottom = "bottom" + Guid.NewGuid().ToString("N");
            var screen = ScreenWindowsTests.Screen("TST", "Only", "1");
            var widget = Widget(out var made);
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Lists(top, widget, "top bar") + Lists(bottom, widget, "bottom bar"));
            shell.Platform.Screens.Add(screen);
            try
            {
                using var topBar = new Bar { Name = top, Reserve = false };
                using var bottomBar = new Bar { Name = bottom, Edge = Edges.Bottom, Reserve = false };
                await NextTurn(shell);
                var bars = BarsOn(shell, screen);
                var topWindow = Assert.Single(bars, window => window.Anchors.HasFlag(Edges.Top));
                var bottomWindow = Assert.Single(bars, window => window.Anchors.HasFlag(Edges.Bottom));
                await Wait.UntilAsync(
                    () => TextsOn(made, topWindow).SequenceEqual(["top bar"]) &&
                          TextsOn(made, bottomWindow).SequenceEqual(["bottom bar"]),
                    "each bar to render its own text");

                await Save(shell, path, Lists(top, widget, "top bar, saved") + Lists(bottom, widget, "bottom bar"));
                await Until(shell, screen, () => !BarsOn(shell, screen).Contains(topWindow), "the top bar to be replaced");
                topWindow = Assert.Single(BarsOn(shell, screen), window => window.Anchors.HasFlag(Edges.Top));
                await Wait.UntilAsync(() => TextsOn(made, topWindow).SequenceEqual(["top bar, saved"]),
                    "the saved text to render");
                Assert.Same(bottomWindow, Assert.Single(BarsOn(shell, screen), window => window.Anchors.HasFlag(Edges.Bottom)));
            }
            finally
            {
                shell.Platform.Screens.Remove(screen);
                File.Delete(path);
            }

            return true;
        });
    }

    [Fact]
    public async Task A_missing_file_is_written_with_the_bar_own_values_commented_out()
    {
        await ShellFixture.On(async shell =>
        {
            var name = "bar" + Guid.NewGuid().ToString("N");
            var widget = Widget(out _);
            var path = PreferencesTests.PathOf(shell);
            if (File.Exists(path))
                File.Delete(path);
            try
            {
                // A section the configuration makes first, in the same turn, posts the check that writes the file.
                using var first = new Reader();
                using var bar = new Bar { Name = name, Edge = Edges.Left, Reserve = false, End = [widget] };
                await Wait.UntilAsync(() => File.Exists(path), "the missing file to be written");

                var text = File.ReadAllText(path);
                Assert.Contains($"[{name}]", text, StringComparison.Ordinal);
                Assert.Contains("# edge = \"left\"", text, StringComparison.Ordinal);
                Assert.Contains($"# end = [{{ widget = \"{widget}\" }}]", text, StringComparison.Ordinal);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }

            return true;
        });
    }

    [Fact]
    public async Task A_bar_is_as_thick_as_its_style_slot_size_unless_Thickness_says_otherwise()
    {
        await ShellFixture.On(async shell =>
        {
            var other = TestStyles.Other();
            var screen = ScreenWindowsTests.Screen("TST", "Only", "1");
            shell.Platform.Screens.Add(screen);
            try
            {
                using var follows = new Bar { Name = "follows" + Guid.NewGuid().ToString("N"), Reserve = false };
                using var own = new Bar
                {
                    Name = "own" + Guid.NewGuid().ToString("N"),
                    Edge = Edges.Bottom,
                    Reserve = false,
                    Style = other
                };
                using var thin = new Bar
                {
                    Name = "thin" + Guid.NewGuid().ToString("N"),
                    Edge = Edges.Left,
                    Reserve = false,
                    Thickness = 20
                };
                await NextTurn(shell);
                var bars = BarsOn(shell, screen);
                var followsBar = Assert.Single(bars, window => window.Anchors == (Edges.Left | Edges.Right | Edges.Top));
                Assert.Equal(32, followsBar.Height);
                var ownBar = Assert.Single(bars, window => window.Anchors.HasFlag(Edges.Bottom) && window.Anchors.HasFlag(Edges.Right));
                Assert.Equal(40, ownBar.Height);
                await ShellFixture.LaidOut(Assert.IsType<ReactorHostControl>(ownBar.Content)).WaitAsync(TestContext.Current.CancellationToken);
                Assert.Equal(global::Windows.UI.Color.FromArgb(0xff, 0x1a, 0x1b, 0x26), BackgroundOf(ownBar.Content!));
                Assert.Equal(20, Assert.Single(bars, window => !window.Anchors.HasFlag(Edges.Right)).Width);

                shell.Style = other;
                await Until(shell, screen, () => !BarsOn(shell, screen).Contains(followsBar), "the bar to take the new style");
                Assert.Equal(40, Assert.Single(BarsOn(shell, screen), window => window.Anchors == (Edges.Left | Edges.Right | Edges.Top)).Height);
                Assert.Contains(ownBar, BarsOn(shell, screen));
            }
            finally
            {
                shell.Style = null;
                shell.Platform.Screens.Remove(screen);
            }

            return true;
        });
    }

    [Fact]
    public async Task A_bar_shows_the_style_background_in_its_window_theme_and_follows_a_switch_while_it_runs()
    {
        await ShellFixture.On(async shell =>
        {
            var scheme = shell.ColorScheme;
            var screen = ScreenWindowsTests.Screen("TST", "Only", "1");
            shell.Platform.Screens.Add(screen);
            try
            {
                shell.ColorScheme = ColorScheme.Light;
                using var bar = new Bar { Name = "bar" + Guid.NewGuid().ToString("N"), Reserve = false };
                await NextTurn(shell);
                var window = Assert.Single(BarsOn(shell, screen));
                var root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                await ShellFixture.LaidOut(root).WaitAsync(TestContext.Current.CancellationToken);
                Assert.Equal(WidgetAreaTests.WindowsColor("Light", "SolidBackgroundFillColorBase"), BackgroundOf(root));

                shell.ColorScheme = ColorScheme.Dark;
                var dark = WidgetAreaTests.WindowsColor("Default", "SolidBackgroundFillColorBase");
                await Wait.UntilAsync(() => BackgroundOf(root) == dark, "the bar to turn dark");
                Assert.Same(window, Assert.Single(BarsOn(shell, screen)));
            }
            finally
            {
                shell.ColorScheme = scheme;
                shell.Platform.Screens.Remove(screen);
            }

            return true;
        });
    }

    [Fact]
    public async Task A_bar_keeps_its_padding_at_its_two_ends_and_takes_its_style_padding_without_one()
    {
        await ShellFixture.On(async shell =>
        {
            var screen = ScreenWindowsTests.Screen("TST", "Only", "1");
            var widget = Widget(out _);
            shell.Platform.Screens.Add(screen);
            try
            {
                using var top = new Bar
                {
                    Name = "top" + Guid.NewGuid().ToString("N"),
                    Reserve = false,
                    Padding = 12,
                    Start = [widget],
                    End = [widget]
                };
                using var side = new Bar
                {
                    Name = "side" + Guid.NewGuid().ToString("N"),
                    Edge = Edges.Left,
                    Reserve = false,
                    Start = [widget],
                    End = [widget]
                };
                using var bottom = new Bar
                {
                    Name = "bottom" + Guid.NewGuid().ToString("N"),
                    Edge = Edges.Bottom,
                    Reserve = false,
                    Style = TestStyles.Other(),
                    Start = [widget],
                    End = [widget]
                };
                await NextTurn(shell);
                var bars = BarsOn(shell, screen);

                var (start, end) =
                    await Ends(Assert.Single(bars, window => window.Anchors == (Edges.Top | Edges.Left | Edges.Right)));
                Assert.Equal(12, start.X, 0.5);
                Assert.Equal(12, end.X, 0.5);
                Assert.Equal(0, start.Y, 0.5);
                Assert.Equal(0, end.Y, 0.5);

                // The built-in style's ShellPadding.
                (start, end) = await Ends(Assert.Single(bars, window => !window.Anchors.HasFlag(Edges.Right)));
                Assert.Equal(4, start.Y, 0.5);
                Assert.Equal(4, end.Y, 0.5);
                Assert.Equal(0, start.X, 0.5);
                Assert.Equal(0, end.X, 0.5);

                // A style with no ShellPadding gives a bar none.
                (start, end) =
                    await Ends(Assert.Single(bars, window => window.Anchors == (Edges.Bottom | Edges.Left | Edges.Right)));
                Assert.Equal(0, start.X, 0.5);
                Assert.Equal(0, end.X, 0.5);
            }
            finally
            {
                shell.Platform.Screens.Remove(screen);
            }

            return true;
        });
    }

    /// <summary>Completes on the turn after the ones already posted, such as a new bar's first.</summary>
    private static Task NextTurn(Shell shell)
    {
        var turn = new TaskCompletionSource();
        shell.Loop.Post(turn.SetResult);
        return turn.Task;
    }

    /// <summary>Saves the preferences file and reads it now, rather than waiting for the file watch.</summary>
    private static async Task Save(Shell shell, string path, string text)
    {
        File.WriteAllText(path, text);
        new Reader().Dispose();
        await NextTurn(shell);
    }

    /// <summary>Waits for a screen's bar to be replaced, and returns the new one.</summary>
    private static async Task<PanelWindow> Replaced(Shell shell, ScreenInfo screen, PanelWindow old)
    {
        await Until(shell, screen, () => BarsOn(shell, screen) is [var only] && only != old,
            "the bar of " + screen.Model + " to be replaced");
        return Assert.Single(BarsOn(shell, screen));
    }

    /// <summary>Waits for <paramref name="done"/>, and fails at once when the platform has dropped the made-up screen.</summary>
    private static Task Until(Shell shell, ScreenInfo screen, Func<bool> done, string what)
    {
        return Wait.UntilAsync(() =>
        {
            Assert.True(shell.Screens.Contains(screen), "The platform read the screens again and dropped the made-up ones.");
            return done();
        }, what);
    }

    private static List<PanelWindow> BarsOn(Shell shell, ScreenInfo screen)
    {
        return [.. shell.Platform.Windows.Select(host => host.Client).OfType<PanelWindow>().Where(window => window.Screen == screen)];
    }

    private static async Task<List<StackPanel>> AreasOf(PanelWindow bar)
    {
        var host = Assert.IsType<ReactorHostControl>(bar.Content);
        await ShellFixture.LaidOut(host).WaitAsync(TestContext.Current.CancellationToken);
        var areas = AreaSessions.Panels(host);
        Assert.Equal(3, areas.Count);
        return areas;
    }

    /// <summary>
    /// How far the start area's first corner is from the bar's, and the end area's last corner from the bar's, once
    /// the bar is laid out.
    /// </summary>
    private static async Task<(Point Start, Point End)> Ends(PanelWindow bar)
    {
        var host = Assert.IsType<ReactorHostControl>(bar.Content);
        var areas = await AreasOf(bar);
        var start = areas[0].TransformToVisual(host).TransformPoint(default);
        var last = areas[2].TransformToVisual(host).TransformPoint(new Point(areas[2].ActualWidth, areas[2].ActualHeight));
        return (start, new Point(host.ActualWidth - last.X, host.ActualHeight - last.Y));
    }

    /// <summary>The color behind what <paramref name="root"/> holds: the first background in its tree.</summary>
    internal static global::Windows.UI.Color? BackgroundOf(DependencyObject root)
    {
        return (First(root) as SolidColorBrush)?.Color;

        static Brush? First(DependencyObject element)
        {
            return element switch
            {
                Border { Background: { } brush } => brush,
                Panel { Background: { } brush } => brush,
                _ => Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(element))
                    .Select(i => First(VisualTreeHelper.GetChild(element, i)))
                    .FirstOrDefault(brush => brush is not null)
            };
        }
    }

    /// <summary>The text of each widget the test widget made in <paramref name="bar"/> that is still in it.</summary>
    private static List<string> TextsOn(List<TextBlock> made, PanelWindow bar)
    {
        var shown = new HashSet<TextBlock>();
        Collect(bar.Content!);
        return [.. made.Where(shown.Contains).Select(view => view.Text)];

        void Collect(DependencyObject node)
        {
            if (node is TextBlock block)
                shown.Add(block);
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                Collect(VisualTreeHelper.GetChild(node, i));
        }
    }

    /// <summary>A widget registered under a new name that shows its <c>text</c> setting, and keeps what it makes.</summary>
    private static string Widget(out List<TextBlock> made)
    {
        var name = Guid.NewGuid().ToString();
        TextSettingWidget.Views = made = [];
        Widgets.Register<TextSettingWidget>(name);
        return name;
    }

    /// <summary>A text block of the placement's <c>text</c> setting, or empty when it has none.</summary>
    private sealed class TextSettingWidget : Component<WidgetContext>
    {
        public static List<TextBlock> Views = [];

        public override Element Render() => XamlHost(() =>
        {
            var view = new TextBlock { Text = Props.Settings.TryGetValue("text", out var text) ? (string)text : "" };
            Views.Add(view);
            return view;
        });
    }

    /// <summary>A bar's section with one widget at the start, and a row for the tall screen on <paramref name="tallEdge"/>.</summary>
    private static string Section(string name, string widget, string edge, string tallEdge, string text)
    {
        return $$"""
            [{{name}}]
            edge = "{{edge}}"
            start = [{ widget = "{{widget}}", text = "{{text}}" }]

            [[{{name}}.screen]]
            manufacturer = "TST"
            model = "Tall"
            serial = "1"
            edge = "{{tallEdge}}"

            """;
    }

    /// <summary>A bar's section with one widget at the start.</summary>
    private static string Lists(string name, string widget, string text)
    {
        return $$"""
            [{{name}}]
            start = [{ widget = "{{widget}}", text = "{{text}}" }]

            """;
    }

    /// <summary>A section with no keys, made to read the file.</summary>
    private sealed partial class Reader() : Preferences("reader");
}
