using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Winshell.ShellTests;

public sealed class WidgetTests
{
    [Fact]
    [Trait("Category", "Window")]
    public async Task A_widget_reads_its_placement_without_props()
    {
        await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            PlacementWidget.Seen = null;
            Widgets.Register<PlacementWidget>(name);
            var style = TestStyles.Marked();
            var settings = new Dictionary<string, object> { ["text"] = "placed" };
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Orientation = Orientation.Vertical,
                Style = style,
                Defaults = [new(name, settings)]
            });
            using var window = new PanelWindow { Width = 160, Height = 90, Content = area.Host, Visible = true };
            await Wait.UntilAsync(() => PlacementWidget.Seen?.Window == window, "the widget to render in its window");

            var seen = PlacementWidget.Seen!;
            Assert.Same(shell, seen.Shell);
            Assert.Equal("placed", seen.Settings["text"]);
            Assert.Equal(Orientation.Vertical, seen.Orientation);
            Assert.Same(style, seen.Style);
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_setting_is_the_placement_value_or_the_fallback_and_a_wrong_type_names_the_key_and_type()
    {
        await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            Widgets.Register<SettingWidget>(name);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Defaults =
                [
                    new(name, new Dictionary<string, object> { ["count"] = 3L, ["on"] = true }),
                    new(name, new Dictionary<string, object> { ["text"] = 5L })
                ]
            });
            using var window = new PanelWindow { Width = 420, Height = 60, Content = area.Host, Visible = true };
            string[] text = [];
            await Wait.UntilAsync(() =>
            {
                text = [.. Texts(area.Host)];
                return text.Length == 2;
            }, "both placements to render");

            Assert.Equal("3|fallback|True|none", text[0]);
            Assert.Equal($"{name}: text must be a string.", text[1]);
            Assert.Contains("UnsupportedWidgetException: text must be a string.", ShellFixture.ReadLog(shell),
                StringComparison.Ordinal);
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_true_or_false_setting_reads_a_number_as_true_unless_it_is_zero_and_refuses_a_string()
    {
        await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            Widgets.Register<SwitchWidget>(name);
            object[] values = [true, false, 2L, 0L, 0.5, "yes"];
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Defaults = [.. values.Select(value => new Placement(name, new Dictionary<string, object> { ["on"] = value }))]
            });
            using var window = new PanelWindow { Width = 640, Height = 60, Content = area.Host, Visible = true };
            string[] text = [];
            await Wait.UntilAsync(() =>
            {
                text = [.. Texts(area.Host)];
                return text.Length == values.Length;
            }, "every placement to render");

            Assert.Equal(["True", "False", "True", "False", "True", $"{name}: on must be true or false."], text);
            Assert.Contains("UnsupportedWidgetException: on must be true or false.", ShellFixture.ReadLog(shell),
                StringComparison.Ordinal);
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task UseNow_renders_each_widget_again_after_the_interval_it_asks_for()
    {
        await ShellFixture.On(async _ =>
        {
            var name = Guid.NewGuid().ToString();
            TickingWidget.Renders.Clear();
            TickingWidget.Times.Clear();
            Widgets.Register<TickingWidget>(name);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Defaults = [Ticking(name, "fast", 60), Ticking(name, "slow", 300)]
            });
            using var window = new PanelWindow { Width = 220, Height = 60, Content = area.Host, Visible = true };
            await Wait.UntilAsync(() => Times("fast").Count >= 2 && Times("slow").Count >= 2, "both clocks to tick in the window");

            TickingWidget.Times.Clear();
            await Wait.UntilAsync(() => Times("slow").Count >= 6, "the slow clock to tick six times");

            AssertEvery(Times("fast"), TimeSpan.FromMilliseconds(60));
            AssertEvery(Times("slow"), TimeSpan.FromMilliseconds(300));
            return true;
        });

        static void AssertEvery(List<DateTime> times, TimeSpan every)
        {
            var mean = (times[^1] - times[0]) / (times.Count - 1);
            Assert.True(mean >= every * 0.9 && mean <= every * 3,
                $"A clock asked to tick every {every.TotalMilliseconds} ms rendered every {mean.TotalMilliseconds} ms.");
        }
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task UseNow_stops_its_timer_when_the_widget_leaves_its_area()
    {
        await ShellFixture.On(async shell =>
        {
            var loop = (DispatcherLoop)shell.Loop;
            var name = Guid.NewGuid().ToString();
            TickingWidget.Renders.Clear();
            TickingWidget.Times.Clear();
            Widgets.Register<TickingWidget>(name);
            var gone = TimeSpan.FromMilliseconds(431);
            var stays = TimeSpan.FromMilliseconds(433);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Defaults = [Ticking(name, "gone", 431), Ticking(name, "stays", 433)]
            });
            using var window = new PanelWindow { Width = 220, Height = 60, Content = area.Host, Visible = true };
            await Wait.UntilAsync(() => Renders("gone") >= 3 && Renders("stays") >= 3, "both clocks to tick");
            Assert.Contains(gone, loop.Waiting());
            Assert.Contains(stays, loop.Waiting());

            area.Set(area.Props with { Defaults = [Ticking(name, "stays", 433)] });
            await Wait.UntilAsync(() => !Texts(area.Host).Contains("gone"), "the first clock to leave the area");
            var last = Renders("gone");
            var since = Renders("stays");
            await Wait.UntilAsync(() => Renders("stays") >= since + 2, "the other clock to tick twice more");
            Assert.DoesNotContain(gone, loop.Waiting());
            Assert.Contains(stays, loop.Waiting());
            Assert.Equal(last, Renders("gone"));
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task UseScreen_is_null_until_the_window_arrives_and_renders_again_when_its_screen_changes()
    {
        await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            ScreenNameWidget.Seen.Clear();
            Widgets.Register<ScreenNameWidget>(name);
            var screen = ScreenWindowsTests.Screen("TST", "UseScreen", "1");
            shell.Platform.Screens.Add(screen);
            try
            {
                using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Defaults = [name] });
                await Wait.UntilAsync(() => ScreenNameWidget.Seen.Count > 0, "the widget to render before its window");
                Assert.Equal((false, (ScreenInfo?)null), ScreenNameWidget.Seen[0]);

                using var window = new PanelWindow { Width = 220, Height = 60, Content = area.Host, Visible = true };
                await Wait.UntilAsync(() => ScreenNameWidget.Seen.Any(seen => seen.Windowed), "the widget to render once the window arrives");
                Assert.Null(ScreenNameWidget.Seen.First(seen => seen.Windowed).Screen);

                window.Screen = screen;
                await Wait.UntilAsync(() => Texts(area.Host).Contains(screen.Name), "the widget to render the window's new screen");
                Assert.Same(screen, ScreenNameWidget.Seen[^1].Screen);
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
    public async Task The_guide_s_message_widget_says_hello_when_its_placement_has_no_text()
    {
        await ShellFixture.On(async _ =>
        {
            var name = Guid.NewGuid().ToString();
            Widgets.Register<MessageWidget.Message>(name);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Defaults = [name, new(name, new Dictionary<string, object> { ["text"] = "Hi" })]
            });
            using var window = new PanelWindow { Width = 320, Height = 60, Content = area.Host, Visible = true };
            string[] text = [];
            await Wait.UntilAsync(() =>
            {
                text = [.. Texts(area.Host)];
                return text.Length == 2;
            }, "both messages to render");

            Assert.Equal(["Hello ·", "Hi ·"], text);
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task The_example_clock_refuses_a_timezone_that_is_not_a_string()
    {
        await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            Widgets.Register<OmarchyStyle.ClockWidget>(name);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Defaults = [new(name, new Dictionary<string, object> { ["timezone"] = 5L })]
            });
            using var window = new PanelWindow { Width = 420, Height = 60, Content = area.Host, Visible = true };
            await Wait.UntilAsync(() => Texts(area.Host).Contains($"{name}: timezone must be a string."),
                "the clock's placeholder");

            Assert.Contains("UnsupportedWidgetException: timezone must be a string.", ShellFixture.ReadLog(shell),
                StringComparison.Ordinal);
            return true;
        });
    }

    private static Placement Ticking(string name, string id, long every) =>
        new(name, new Dictionary<string, object> { ["id"] = id, ["every"] = every });

    private static int Renders(string id) => TickingWidget.Renders.GetValueOrDefault(id);

    private static List<DateTime> Times(string id) => TickingWidget.Times.GetValueOrDefault(id) ?? [];
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

    private sealed record Placed(
        Shell Shell,
        ShellWindow? Window,
        IReadOnlyDictionary<string, object> Settings,
        Orientation Orientation,
        ShellStyle? Style);

    private sealed class PlacementWidget : Widget
    {
        public static Placed? Seen;

        public override Element Render()
        {
            Seen = new Placed(Shell, Window, Settings, Orientation, Style);
            return TextBlock("placed");
        }
    }

    private sealed class SettingWidget : Widget
    {
        public override Element Render() =>
            TextBlock($"{Setting("count", 0)}|{Setting("missing", "fallback")}|{Setting("on", false)}|{Setting("text", "none")}");
    }

    private sealed class SwitchWidget : Widget
    {
        public override Element Render() => TextBlock($"{Setting("on", false)}");
    }

    private sealed class TickingWidget : Widget
    {
        public static readonly Dictionary<string, int> Renders = [];
        public static readonly Dictionary<string, List<DateTime>> Times = [];

        public override Element Render()
        {
            var now = UseNow(TimeSpan.FromMilliseconds(Setting("every", 20)));
            var id = Setting("id", "");
            Renders[id] = Renders.GetValueOrDefault(id) + 1;
            if (Window is not null)
            {
                if (!Times.TryGetValue(id, out var times))
                    Times[id] = times = [];
                times.Add(now);
            }

            return TextBlock(id);
        }
    }

    private sealed class ScreenNameWidget : Widget
    {
        public static readonly List<(bool Windowed, ScreenInfo? Screen)> Seen = [];

        public override Element Render()
        {
            var screen = UseScreen();
            Seen.Add((Window is not null, screen));
            return TextBlock(screen?.Name ?? "no screen");
        }
    }
}
