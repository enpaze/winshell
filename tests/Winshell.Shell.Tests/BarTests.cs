using System.Globalization;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

[TestClass]
[TestCategory("Window")]
public sealed class BarTests
{
    [TestMethod]
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
                using var bar = shell.Add(new Bar { Name = "bar" + Guid.NewGuid().ToString("N"), Reserve = false, Center = [widget] });
                await NextTurn(shell);
                var firstBar = Assert.ContainsSingle(BarsOn(shell, first));
                Assert.AreEqual((Edge.Top, PanelAlign.Stretch), (firstBar.Edge, firstBar.Align));
                Assert.AreEqual(32, firstBar.Height);
                foreach (var area in await AreasOf(firstBar))
                    Assert.AreEqual(Orientation.Horizontal, area.Orientation);
                await UntilShown(() => TextsOn(made, firstBar).SequenceEqual([""]), "the center widget to render", firstBar);

                screens.Add(second);
                Assert.ContainsSingle(BarsOn(shell, second));
                screens.Remove(second);
                Assert.IsEmpty(BarsOn(shell, second));
                Assert.AreSame(firstBar, Assert.ContainsSingle(BarsOn(shell, first)));

                bar.Dispose();
                Assert.IsEmpty(BarsOn(shell, first));
            }
            finally
            {
                screens.Remove(first);
                screens.Remove(second);
            }

            return true;
        });
    }

    [TestMethod]
    public async Task A_bar_shows_nothing_until_it_is_added()
    {
        await ShellFixture.On(async shell =>
        {
            var screen = ScreenWindowsTests.Screen("TST", "Unadded", "1");
            shell.Platform.Screens.Add(screen);
            try
            {
                using var bar = new Bar { Name = "bar" + Guid.NewGuid().ToString("N"), Reserve = false };
                await NextTurn(shell);
                Assert.IsEmpty(BarsOn(shell, screen));

                shell.Add(bar);
                await NextTurn(shell);
                Assert.ContainsSingle(BarsOn(shell, screen));
            }
            finally
            {
                shell.Platform.Screens.Remove(screen);
            }

            return true;
        });
    }

    [TestMethod]
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
                using var bar = shell.Add(new Bar
                {
                    Name = name,
                    Reserve = false,
                    Start = [new(widget, new Dictionary<string, object> { ["text"] = "from the code" })]
                });
                await NextTurn(shell);
                var wideBar = Assert.ContainsSingle(BarsOn(shell, wide));
                Assert.AreEqual((Edge.Top, PanelAlign.Stretch), (wideBar.Edge, wideBar.Align));
                Assert.IsFalse(wideBar.Reserve);
                await UntilShown(() => TextsOn(made, wideBar).SequenceEqual(["from the file"]),
                    "the file's text to render", wideBar);
                var tallBar = Assert.ContainsSingle(BarsOn(shell, tall));
                Assert.AreEqual((Edge.Left, PanelAlign.Stretch), (tallBar.Edge, tallBar.Align));
                Assert.AreEqual(32, tallBar.Width);
                foreach (var area in await AreasOf(tallBar))
                    Assert.AreEqual(Orientation.Vertical, area.Orientation);

                await Save(shell, path, Section(name, widget, "top", "right", "from the file"));
                tallBar = await Replaced(shell, tall, tallBar);
                Assert.AreEqual((Edge.Right, PanelAlign.Stretch), (tallBar.Edge, tallBar.Align));
                Assert.AreSame(wideBar, Assert.ContainsSingle(BarsOn(shell, wide)));

                await Save(shell, path, Section(name, widget, "top", "middle", "from the file"));
                tallBar = await Replaced(shell, tall, tallBar);
                Assert.AreEqual((Edge.Top, PanelAlign.Stretch), (tallBar.Edge, tallBar.Align));
                Assert.Contains("edge in its [[" + name + ".screen]] row must be", ShellFixture.ReadLog(shell),
                    StringComparison.Ordinal);

                await Save(shell, path, Section(name, widget, "bottom", "right", "saved"));
                wideBar = await Replaced(shell, wide, wideBar);
                tallBar = await Replaced(shell, tall, tallBar);
                Assert.AreEqual((Edge.Bottom, PanelAlign.Stretch), (wideBar.Edge, wideBar.Align));
                foreach (var area in await AreasOf(wideBar))
                    Assert.AreEqual(Orientation.Horizontal, area.Orientation);
                Assert.AreEqual((Edge.Right, PanelAlign.Stretch), (tallBar.Edge, tallBar.Align));
                await UntilShown(
                    () => TextsOn(made, wideBar).SequenceEqual(["saved"]) && TextsOn(made, tallBar).SequenceEqual(["saved"]),
                    "the saved text to render", wideBar, tallBar);
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

    [TestMethod]
    public async Task A_bar_section_and_its_screen_row_take_bare_widget_names_mixed_with_tables()
    {
        await ShellFixture.On(async shell =>
        {
            var name = "bar" + Guid.NewGuid().ToString("N");
            var wide = ScreenWindowsTests.Screen("TST", "Wide", "1");
            var tall = ScreenWindowsTests.Screen("TST", "Tall", "1");
            var widget = Widget(out var made);
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $$"""
                [{{name}}]
                start = ["{{widget}}", { widget = "{{widget}}", text = "section" }]

                [[{{name}}.screen]]
                manufacturer = "TST"
                model = "Tall"
                serial = "1"
                end = [{ widget = "{{widget}}", text = "row" }, "{{widget}}"]

                """);
            shell.Platform.Screens.Add(wide);
            shell.Platform.Screens.Add(tall);
            try
            {
                var logLength = ShellFixture.ReadLog(shell).Length;
                using var bar = shell.Add(new Bar { Name = name, Reserve = false });
                await NextTurn(shell);
                var wideBar = Assert.ContainsSingle(BarsOn(shell, wide));
                var tallBar = Assert.ContainsSingle(BarsOn(shell, tall));
                await UntilShown(
                    () => TextsOn(made, wideBar).Order().SequenceEqual(["", "section"]) &&
                          TextsOn(made, tallBar).Order().SequenceEqual(["", "", "row", "section"]),
                    "the bare names and the tables to render", wideBar, tallBar);
                Assert.DoesNotContain("must be a list", ShellFixture.ReadLog(shell)[logLength..], StringComparison.Ordinal);
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

    [TestMethod]
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
                using var topBar = shell.Add(new Bar { Name = top, Reserve = false });
                using var bottomBar = shell.Add(new Bar { Name = bottom, Edge = Edge.Bottom, Reserve = false });
                await NextTurn(shell);
                var bars = BarsOn(shell, screen);
                var topWindow = Assert.ContainsSingle(window => window.Edge == Edge.Top, bars);
                var bottomWindow = Assert.ContainsSingle(window => window.Edge == Edge.Bottom, bars);
                await UntilShown(
                    () => TextsOn(made, topWindow).SequenceEqual(["top bar"]) &&
                          TextsOn(made, bottomWindow).SequenceEqual(["bottom bar"]),
                    "each bar to render its own text", topWindow, bottomWindow);

                await Save(shell, path, Lists(top, widget, "top bar, saved") + Lists(bottom, widget, "bottom bar"));
                await Until(shell, screen, () => !BarsOn(shell, screen).Contains(topWindow), "the top bar to be replaced");
                topWindow = Assert.ContainsSingle(window => window.Edge == Edge.Top, BarsOn(shell, screen));
                await UntilShown(() => TextsOn(made, topWindow).SequenceEqual(["top bar, saved"]),
                    "the saved text to render", topWindow);
                Assert.AreSame(bottomWindow, Assert.ContainsSingle(window => window.Edge == Edge.Bottom, BarsOn(shell, screen)));
            }
            finally
            {
                shell.Platform.Screens.Remove(screen);
                File.Delete(path);
            }

            return true;
        });
    }

    [TestMethod]
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
                using var first = new Reader();
                using var bar = shell.Add(new Bar
                {
                    Name = name,
                    Edge = Edge.Left,
                    Reserve = false,
                    End = [widget, new Placement(widget, new Dictionary<string, object> { ["text"] = "Hi" })]
                });
                await Wait.UntilAsync(
                    () => File.Exists(path) && File.ReadAllText(path).Contains($"[{name}]", StringComparison.Ordinal),
                    "the missing file to be written with the bar's section");

                var text = File.ReadAllText(path);
                Assert.Contains($"[{name}]", text, StringComparison.Ordinal);
                Assert.Contains("# edge = \"left\"", text, StringComparison.Ordinal);
                var end = Assert.ContainsSingle(line => line.StartsWith("# end = ", StringComparison.Ordinal), text.Split('\n'));
                Assert.AreEqual($"# end = [\"{widget}\", {{ widget = \"{widget}\", text = \"Hi\" }}]", end);
                Assert.IsTrue(TomlDocument.TryParse(path, end[2..], out var document, out var error), error);
                var placements = Placement.Parse(document.Values["end"]!);
                Assert.AreSequenceEqual([widget, widget], placements.Select(placement => placement.Widget));
                Assert.IsEmpty(placements[0].Settings!);
                Assert.AreSequenceEqual(["text"], placements[1].Settings!.Keys);
                Assert.AreEqual("Hi", placements[1].Settings!["text"]);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }

            return true;
        });
    }

    [TestMethod]
    public async Task A_bar_is_as_thick_as_its_style_slot_size_unless_Thickness_says_otherwise()
    {
        await ShellFixture.On(async shell =>
        {
            var other = TestStyles.Other();
            var screen = ScreenWindowsTests.Screen("TST", "Only", "1");
            shell.Platform.Screens.Add(screen);
            try
            {
                using var follows = shell.Add(new Bar { Name = "follows" + Guid.NewGuid().ToString("N"), Reserve = false });
                using var own = shell.Add(new Bar
                {
                    Name = "own" + Guid.NewGuid().ToString("N"),
                    Edge = Edge.Bottom,
                    Reserve = false,
                    Style = other
                });
                using var thin = shell.Add(new Bar
                {
                    Name = "thin" + Guid.NewGuid().ToString("N"),
                    Edge = Edge.Left,
                    Reserve = false,
                    Thickness = 20
                });
                await NextTurn(shell);
                var bars = BarsOn(shell, screen);
                var followsBar = Assert.ContainsSingle(window => window is { Edge: Edge.Top, Align: PanelAlign.Stretch }, bars);
                Assert.AreEqual(32, followsBar.Height);
                var ownBar = Assert.ContainsSingle(window => window is { Edge: Edge.Bottom, Align: PanelAlign.Stretch }, bars);
                Assert.AreEqual(40, ownBar.Height);
                await ShellFixture.LaidOut(Assert.IsExactInstanceOfType<ReactorHostControl>(ownBar.Content)).WaitAsync(CurrentTest.Cancellation);
                Assert.AreEqual(global::Windows.UI.Color.FromArgb(0xff, 0x1a, 0x1b, 0x26), BackgroundOf(ownBar.Content!));
                Assert.AreEqual(20, Assert.ContainsSingle(window => window.Edge == Edge.Left, bars).Width);

                shell.Style = other;
                await NextTurn(shell);
                Assert.AreSame(followsBar, Assert.ContainsSingle(
                    window => window is { Edge: Edge.Top, Align: PanelAlign.Stretch }, BarsOn(shell, screen)));
                Assert.AreEqual(40, followsBar.Height);
                Assert.Contains(ownBar, BarsOn(shell, screen));
            }
            finally
            {
                shell.Style = new();
                shell.Platform.Screens.Remove(screen);
            }

            return true;
        });
    }

    [TestMethod]
    public async Task A_reserving_bar_takes_its_thickness_from_the_work_area_on_its_edge()
    {
        await ShellFixture.On(async shell =>
        {
            var before = shell.Screens.ToDictionary(screen => screen.Id, screen => screen.WorkArea);
            var bar = shell.Add(new Bar
            {
                Name = "reserving" + Guid.NewGuid().ToString("N"),
                Edge = Edge.Left,
                Reserve = true,
                Thickness = 40
            });
            try
            {
                await Wait.UntilAsync(() => shell.Screens.All(screen => screen.WorkArea.Left > before[screen.Id].Left),
                    "every screen's work area to give the bar its strip");
                foreach (var screen in shell.Screens)
                {
                    var was = before[screen.Id];
                    Assert.AreEqual(was.Left + 40, screen.WorkArea.Left, 1.0);
                    Assert.AreEqual(was.Right, screen.WorkArea.Right, 1.0);
                    Assert.AreEqual(was.Top, screen.WorkArea.Top, 1.0);
                    Assert.AreEqual(was.Bottom, screen.WorkArea.Bottom, 1.0);
                }
            }
            finally
            {
                bar.Dispose();
            }

            await Wait.UntilAsync(() => shell.Screens.All(screen => screen.WorkArea == before[screen.Id]),
                "the work area to come back when the bar goes");
            return true;
        });
    }

    [TestMethod]
    public async Task A_color_change_keeps_the_bar_window_and_the_widget_state()
    {
        await ShellFixture.On(async shell =>
        {
            var paint = new ShellStyle
            {
                Background = global::Windows.UI.Color.FromArgb(0xff, 0x1a, 0x1b, 0x26),
                Foreground = global::Windows.UI.Color.FromArgb(0xff, 0xff, 0xff, 0xff),
                Alert = global::Windows.UI.Color.FromArgb(0xff, 0xff, 0xa5, 0x00)
            };
            var screen = ScreenWindowsTests.Screen("TST", "Only", "1");
            var widget = CountingWidget.Register(out var made);
            shell.Platform.Screens.Add(screen);
            try
            {
                using var bar = shell.Add(new Bar
                {
                    Name = "keeps" + Guid.NewGuid().ToString("N"),
                    Reserve = false,
                    Center = [widget]
                });
                await NextTurn(shell);
                var window = Assert.ContainsSingle(BarsOn(shell, screen));
                Assert.AreEqual(32, window.Height);
                await UntilShown(() => TextsOn(made, window).SequenceEqual(["0"]), "the widget to render", window);
                var state = CountingWidget.Live;
                Assert.IsNotNull(state);
                state.Value = 1;

                shell.Style = paint;
                await NextTurn(shell);
                Assert.AreSame(window, Assert.ContainsSingle(BarsOn(shell, screen)));
                Assert.AreSame(state, CountingWidget.Live);
                await UntilShown(() =>
                    ReferenceEquals(Assert.ContainsSingle(BarsOn(shell, screen)), window) &&
                    ReferenceEquals(CountingWidget.Live, state) &&
                    BackgroundOf(window.Content!) == paint.Background,
                    "the bar to take the new color and keep the widget", window);
                Assert.AreEqual(1, state.Value);
                Assert.AreEqual(32, window.Height);
            }
            finally
            {
                shell.Style = new();
                shell.Platform.Screens.Remove(screen);
                CountingWidget.Live = null;
            }

            return true;
        });
    }

    [TestMethod]
    public async Task A_bar_shows_the_style_background_in_its_window_theme_and_follows_a_switch_while_it_runs()
    {
        await ShellFixture.On(async shell =>
        {
            var appearance = shell.Appearance;
            var screen = ScreenWindowsTests.Screen("TST", "Only", "1");
            shell.Platform.Screens.Add(screen);
            try
            {
                shell.Appearance = Appearance.Light;
                using var bar = shell.Add(new Bar { Name = "bar" + Guid.NewGuid().ToString("N"), Reserve = false });
                await NextTurn(shell);
                var window = Assert.ContainsSingle(BarsOn(shell, screen));
                var root = Assert.IsInstanceOfType<FrameworkElement>(window.Content);
                await ShellFixture.LaidOut(root).WaitAsync(CurrentTest.Cancellation);
                Assert.AreEqual(WidgetAreaTests.WindowsColor("Light", "SolidBackgroundFillColorBase"), BackgroundOf(root));

                shell.Appearance = Appearance.Dark;
                var dark = WidgetAreaTests.WindowsColor("Default", "SolidBackgroundFillColorBase");
                await UntilShown(() => BackgroundOf(root) == dark, "the bar to turn dark", window);
                Assert.AreSame(window, Assert.ContainsSingle(BarsOn(shell, screen)));
            }
            finally
            {
                shell.Appearance = appearance;
                shell.Platform.Screens.Remove(screen);
            }

            return true;
        });
    }

    [TestMethod]
    public async Task A_bar_keeps_its_padding_at_its_two_ends_and_takes_its_style_padding_without_one()
    {
        await ShellFixture.On(async shell =>
        {
            var screen = ScreenWindowsTests.Screen("TST", "Only", "1");
            var widget = Widget(out _);
            shell.Platform.Screens.Add(screen);
            try
            {
                using var top = shell.Add(new Bar
                {
                    Name = "top" + Guid.NewGuid().ToString("N"),
                    Reserve = false,
                    Padding = 12,
                    Start = [widget],
                    End = [widget]
                });
                using var side = shell.Add(new Bar
                {
                    Name = "side" + Guid.NewGuid().ToString("N"),
                    Edge = Edge.Left,
                    Reserve = false,
                    Start = [widget],
                    End = [widget]
                });
                using var bottom = shell.Add(new Bar
                {
                    Name = "bottom" + Guid.NewGuid().ToString("N"),
                    Edge = Edge.Bottom,
                    Reserve = false,
                    Style = TestStyles.Other(),
                    Start = [widget],
                    End = [widget]
                });
                await NextTurn(shell);
                var bars = BarsOn(shell, screen);

                var (start, end) =
                    await Ends(Assert.ContainsSingle(window => window is { Edge: Edge.Top, Align: PanelAlign.Stretch }, bars));
                Assert.AreEqual(12, start.X, 0.5);
                Assert.AreEqual(12, end.X, 0.5);
                Assert.AreEqual(0, start.Y, 0.5);
                Assert.AreEqual(0, end.Y, 0.5);

                (start, end) = await Ends(Assert.ContainsSingle(window => window.Edge == Edge.Left, bars));
                Assert.AreEqual(4, start.Y, 0.5);
                Assert.AreEqual(4, end.Y, 0.5);
                Assert.AreEqual(0, start.X, 0.5);
                Assert.AreEqual(0, end.X, 0.5);

                (start, end) =
                    await Ends(Assert.ContainsSingle(window => window is { Edge: Edge.Bottom, Align: PanelAlign.Stretch }, bars));
                Assert.AreEqual(0, start.X, 0.5);
                Assert.AreEqual(0, end.X, 0.5);
            }
            finally
            {
                shell.Platform.Screens.Remove(screen);
            }

            return true;
        });
    }

    private static Task NextTurn(Shell shell)
    {
        var turn = new TaskCompletionSource();
        shell.Loop.Post(turn.SetResult);
        return turn.Task;
    }

    private static async Task Save(Shell shell, string path, string text)
    {
        File.WriteAllText(path, text);
        new Reader().Dispose();
        await NextTurn(shell);
    }

    private static async Task<PanelWindow> Replaced(Shell shell, ScreenInfo screen, PanelWindow old)
    {
        await Until(shell, screen, () => BarsOn(shell, screen) is [var only] && only != old,
            "the bar of " + screen.Model + " to be replaced");
        return Assert.ContainsSingle(BarsOn(shell, screen));
    }

    private static Task UntilShown(Func<bool> done, string what, params PanelWindow[] bars)
    {
        return Wait.UntilAsync(async () =>
        {
            foreach (var bar in bars)
            {
                if (bar.Content is FrameworkElement root)
                    await ShellFixture.LaidOut(root).WaitAsync(CurrentTest.Cancellation);
            }

            return done();
        }, what);
    }

    private static Task Until(Shell shell, ScreenInfo screen, Func<bool> done, string what)
    {
        return Wait.UntilAsync(() =>
        {
            Assert.IsTrue(shell.Screens.Contains(screen), "The platform read the screens again and dropped the made-up ones.");
            return done();
        }, what);
    }

    private static List<PanelWindow> BarsOn(Shell shell, ScreenInfo screen)
    {
        return [.. shell.Platform.Windows.Select(host => host.Client).OfType<PanelWindow>().Where(window => window.Screen == screen)];
    }

    private static async Task<List<StackPanel>> AreasOf(PanelWindow bar)
    {
        var host = Assert.IsExactInstanceOfType<ReactorHostControl>(bar.Content);
        await ShellFixture.LaidOut(host).WaitAsync(CurrentTest.Cancellation);
        var stacks = new List<StackPanel>();
        Walk(host);
        var areas = stacks.Where(stack => !InsideAnother(stack, stacks)).ToList();
        Assert.AreEqual(3, areas.Count);
        var vertical = bar.Edge is Edge.Left or Edge.Right;
        areas.Sort((left, right) =>
        {
            var leftAt = left.TransformToVisual(host).TransformPoint(default);
            var rightAt = right.TransformToVisual(host).TransformPoint(default);
            return vertical ? leftAt.Y.CompareTo(rightAt.Y) : leftAt.X.CompareTo(rightAt.X);
        });
        return areas;

        void Walk(DependencyObject node)
        {
            if (node is StackPanel panel)
                stacks.Add(panel);
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                Walk(VisualTreeHelper.GetChild(node, i));
        }

        static bool InsideAnother(StackPanel stack, List<StackPanel> all)
        {
            for (var parent = VisualTreeHelper.GetParent(stack); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is StackPanel outer && all.Contains(outer))
                    return true;
            }
            return false;
        }
    }

    private static async Task<(Point Start, Point End)> Ends(PanelWindow bar)
    {
        var host = Assert.IsExactInstanceOfType<ReactorHostControl>(bar.Content);
        var areas = await AreasOf(bar);
        var start = areas[0].TransformToVisual(host).TransformPoint(default);
        var last = areas[2].TransformToVisual(host).TransformPoint(new Point(areas[2].ActualWidth, areas[2].ActualHeight));
        return (start, new Point(host.ActualWidth - last.X, host.ActualHeight - last.Y));
    }

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

    private static string Widget(out List<TextBlock> made)
    {
        var name = Guid.NewGuid().ToString();
        TextSettingWidget.Views = made = [];
        Shell.Current.WidgetRegistry.Add<TextSettingWidget>(name);
        return name;
    }

    private sealed class TextSettingWidget : Widget
    {
        public static List<TextBlock> Views = [];

        public override Element Render() => XamlHost(() =>
        {
            var view = new TextBlock { Text = Setting("text", "") };
            Views.Add(view);
            return view;
        });
    }

    private sealed class CountingWidget : Widget
    {
        public static List<TextBlock> Views = [];

        public static Count? Live;

        public static string Register(out List<TextBlock> made)
        {
            var name = Guid.NewGuid().ToString();
            Views = made = [];
            Live = null;
            Shell.Current.WidgetRegistry.Add<CountingWidget>(name);
            return name;
        }

        public override Element Render()
        {
            var held = UseRef<Count?>(null);
            if (held.Current is null)
                held.Current = new Count();
            var mine = held.Current;
            Live = mine;
            UseEffect(() => () =>
            {
                if (ReferenceEquals(Live, mine))
                    Live = null;
            }, false);
            return XamlHost(() =>
            {
                var view = new TextBlock { Text = mine.Value.ToString(CultureInfo.InvariantCulture) };
                Views.Add(view);
                return view;
            });
        }

        internal sealed class Count
        {
            public int Value;
        }
    }

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

    private static string Lists(string name, string widget, string text)
    {
        return $$"""
            [{{name}}]
            start = [{ widget = "{{widget}}", text = "{{text}}" }]

            """;
    }

    private sealed partial class Reader() : Preferences("reader");
}
