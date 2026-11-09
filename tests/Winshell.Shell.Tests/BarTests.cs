using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Winshell.ShellTests;

/// <summary>
/// A bar on every screen, from its properties, its section of the preferences file and each screen's row. The screens
/// are made up and added to the shell's own list, as <see cref="ScreenWindowsTests"/> does. A panel on a screen
/// Windows doesn't have is placed on the primary screen, so only one test reserves an edge: a reservation moves the
/// work area, and the platform then reads the screens again and drops the made-up ones.
/// </summary>
public sealed class BarTests
{
    [Fact]
    public async Task A_bar_shows_on_each_screen_reserves_its_edge_and_follows_a_screen_that_comes_or_goes()
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
                using var bar = new Bar { Name = "bar" + Guid.NewGuid().ToString("N"), Center = [widget] };
                await NextTurn(shell);
                // Everything from here runs in this turn, before the platform reads the screens again.
                var firstBar = Assert.Single(BarsOn(shell, first));
                Assert.Equal(Edges.Left | Edges.Right | Edges.Top, firstBar.Anchors);
                Assert.Equal(32, firstBar.Height);
                Assert.Equal(32, firstBar.ReservedSize);
                Assert.All(AreasOf(firstBar), area => Assert.Equal(Orientation.Horizontal, area.Orientation));
                Assert.Equal([""], TextsOn(made, firstBar));

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
                Assert.Equal(["from the file"], TextsOn(made, wideBar));
                var tallBar = Assert.Single(BarsOn(shell, tall));
                Assert.Equal(Edges.Left | Edges.Top | Edges.Bottom, tallBar.Anchors);
                Assert.Equal(32, tallBar.Width);
                Assert.All(AreasOf(tallBar), area => Assert.Equal(Orientation.Vertical, area.Orientation));

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
                Assert.All(AreasOf(wideBar), area => Assert.Equal(Orientation.Horizontal, area.Orientation));
                Assert.Equal(["saved"], TextsOn(made, wideBar));
                Assert.Equal(Edges.Right | Edges.Top | Edges.Bottom, tallBar.Anchors);
                Assert.Equal(["saved"], TextsOn(made, tallBar));
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
                Assert.Equal(["top bar"], TextsOn(made, topWindow));
                Assert.Equal(["bottom bar"], TextsOn(made, bottomWindow));

                await Save(shell, path, Lists(top, widget, "top bar, saved") + Lists(bottom, widget, "bottom bar"));
                await Wait.UntilAsync(() => !BarsOn(shell, screen).Contains(topWindow), "the top bar to be replaced");
                topWindow = Assert.Single(BarsOn(shell, screen), window => window.Anchors.HasFlag(Edges.Top));
                Assert.Equal(["top bar, saved"], TextsOn(made, topWindow));
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
    public async Task A_bar_is_as_thick_as_its_style_slot_size_unless_Thickness_says_otherwise()
    {
        await ShellFixture.On(async shell =>
        {
            var other = new Uri("ms-appx:///OtherStyle.xaml");
            var screen = ScreenWindowsTests.Screen("TST", "Only", "1");
            shell.Platform.Screens.Add(screen);
            try
            {
                using var follows = new Bar { Name = "follows" + Guid.NewGuid().ToString("N"), Reserve = false };
                using var own = new Bar
                {
                    Name = "own" + Guid.NewGuid().ToString("N"), Edge = Edges.Bottom, Reserve = false, Style = other
                };
                using var thin = new Bar
                {
                    Name = "thin" + Guid.NewGuid().ToString("N"), Edge = Edges.Left, Reserve = false, Thickness = 20
                };
                await NextTurn(shell);
                var bars = BarsOn(shell, screen);
                var followsBar = Assert.Single(bars, window => window.Anchors == (Edges.Left | Edges.Right | Edges.Top));
                Assert.Equal(32, followsBar.Height);
                var ownBar = Assert.Single(bars, window => window.Anchors.HasFlag(Edges.Bottom) && window.Anchors.HasFlag(Edges.Right));
                Assert.Equal(40, ownBar.Height);
                Assert.Equal(global::Windows.UI.Color.FromArgb(0xff, 0x1a, 0x1b, 0x26),
                    Assert.IsType<SolidColorBrush>(Assert.IsType<Grid>(ownBar.Content).Background).Color);
                Assert.Equal(20, Assert.Single(bars, window => !window.Anchors.HasFlag(Edges.Right)).Width);

                shell.Style = other;
                await Wait.UntilAsync(() => !BarsOn(shell, screen).Contains(followsBar), "the bar to take the shell's new style");
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
        await Wait.UntilAsync(() =>
        {
            Assert.True(shell.Screens.Contains(screen), "The platform read the screens again and dropped the made-up ones.");
            return BarsOn(shell, screen) is [var only] && only != old;
        }, "the bar of " + screen.Model + " to be replaced");
        return Assert.Single(BarsOn(shell, screen));
    }

    private static List<PanelWindow> BarsOn(Shell shell, ScreenInfo screen)
    {
        return [.. shell.Platform.Windows.Select(host => host.Client).OfType<PanelWindow>().Where(window => window.Screen == screen)];
    }

    private static IEnumerable<WidgetArea> AreasOf(PanelWindow bar)
    {
        return Assert.IsType<Grid>(bar.Content).Children.Cast<WidgetArea>();
    }

    /// <summary>The text of each widget the test widget made in <paramref name="bar"/> that is still in it.</summary>
    private static List<string> TextsOn(List<TextBlock> made, PanelWindow bar)
    {
        var widgets = AreasOf(bar).SelectMany(area => Assert.IsAssignableFrom<Panel>(area.Content).Children).ToList();
        return [.. made.Where(widgets.Contains).Select(view => view.Text)];
    }

    /// <summary>A widget registered under a new name that shows its <c>text</c> setting, and keeps what it makes.</summary>
    private static string Widget(out List<TextBlock> made)
    {
        var name = Guid.NewGuid().ToString();
        var views = made = [];
        Widgets.Register(name, context =>
        {
            var view = new TextBlock { Text = context.Settings.TryGetValue("text", out var text) ? (string)text : "" };
            views.Add(view);
            return view;
        });
        return name;
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
