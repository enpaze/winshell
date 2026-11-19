using Windows.Foundation;

namespace Winshell.ShellTests;

public sealed class ScreenWindowsTests
{
    [Fact]
    public Task A_screen_read_that_one_action_asks_for_is_done_before_the_next_action_starts()
    {
        return ReadBeforeTheNextAction(platform => platform.ScreensMayHaveChanged());
    }

    [Fact]
    public Task A_work_area_relayout_that_one_action_asks_for_is_done_before_the_next_action_starts()
    {
        return ReadBeforeTheNextAction(platform => platform.WorkAreaMayHaveChanged());
    }

    private static async Task ReadBeforeTheNextAction(Action<ShellPlatform> ask)
    {
        var madeUp = Screen("TST", "Only", "1");
        await ShellFixture.On(shell =>
        {
            shell.Platform.Screens.Add(madeUp);
            ask(shell.Platform);
            return true;
        });

        await ShellFixture.On(shell =>
        {
            var notRead = shell.Platform.Screens.Contains(madeUp);
            shell.Platform.Screens.Remove(madeUp);
            Assert.False(notRead, "The screen read the first action asked for had not run when the next action started.");
            return true;
        });
    }

    [Fact]
    public async Task A_screen_has_a_window_from_when_it_appears_until_it_goes_or_another_monitor_takes_its_id()
    {
        await ShellFixture.On(shell =>
        {
            var screens = shell.Platform.Screens;
            var first = Screen("TST", "First", "1");
            var second = Screen("TST", "Second", "2");
            var made = new List<Window>();
            screens.Add(first);
            try
            {
                using var windows = shell.Add(new ScreenWindows("windows" + Guid.NewGuid().ToString("N"), Record(made)));
                var firstWindow = Assert.Single(made, window => window.Screen == first);

                screens.Add(second);
                var secondWindow = Assert.Single(made, window => window.Screen == second);

                var resized = first with { Geometry = new Rect(0, 0, 2560, 1440), DevicePixelRatio = 1.5 };
                screens[screens.IndexOf(first)] = resized;
                screens.Remove(second);
                Assert.True(secondWindow.Disposed);
                Assert.False(firstWindow.Disposed);
                Assert.Equal([firstWindow, secondWindow], made.Where(window => window.Screen.Id == first.Id || window.Screen.Id == second.Id));

                var other = resized with { Model = "Other", SerialNumber = "3" };
                screens[screens.IndexOf(resized)] = other;
                Assert.True(firstWindow.Disposed);
                Assert.Single(made, window => window.Screen == other);

                windows.Dispose();
                Assert.All(made, window => Assert.True(window.Disposed));
            }
            finally
            {
                foreach (var screen in screens.Where(screen => screen.Id == first.Id || screen.Id == second.Id).ToArray())
                    screens.Remove(screen);
            }

            return true;
        });
    }

    [Fact]
    public async Task Screen_windows_make_nothing_until_added_and_then_start_each_window_they_make()
    {
        var (before, starts) = await ShellFixture.On(shell =>
        {
            var screen = Screen("TST", "Added", "1");
            var made = new List<Window>();
            shell.Platform.Screens.Add(screen);
            try
            {
                using var windows = new ScreenWindows("windows" + Guid.NewGuid().ToString("N"), Record(made));
                var madeBefore = made.Count;
                shell.Add(windows);
                return (madeBefore, Assert.Single(made, window => window.Screen == screen).Starts);
            }
            finally
            {
                shell.Platform.Screens.Remove(screen);
            }
        });

        Assert.Equal(0, before);
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task A_screen_gets_its_row_by_manufacturer_model_and_serial_and_a_changed_row_replaces_only_its_window()
    {
        await ShellFixture.On(async shell =>
        {
            var name = "dock" + Guid.NewGuid().ToString("N");
            var wide = Screen("TST", "Wide", "1");
            var otherWide = Screen("TST", "Wide", "2");
            var tall = Screen("TST", "Tall", "1");
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Rows(name, "left"));
            var made = new List<Window>();
            foreach (var screen in (ScreenInfo[])[wide, otherWide, tall])
                shell.Platform.Screens.Add(screen);
            try
            {
                using var windows = shell.Add(new ScreenWindows(name, Record(made)));
                var wideWindow = Assert.Single(made, window => window.Screen == wide);
                var otherWideWindow = Assert.Single(made, window => window.Screen == otherWide);
                var tallWindow = Assert.Single(made, window => window.Screen == tall);
                Assert.Equal("left", wideWindow.Row?["edge"]);
                Assert.Null(otherWideWindow.Row);
                Assert.Equal("right", tallWindow.Row?["edge"]);

                File.WriteAllText(path, Rows(name, "bottom"));
                new Section(name).Dispose();
                await Wait.UntilAsync(() =>
                {
                    Assert.True(shell.Screens.Contains(wide), "The platform refreshed the screens and dropped the made-up ones.");
                    return made.Count(window => window.Screen == wide) == 2;
                }, "the changed row to replace its screen's window");
                Assert.True(wideWindow.Disposed);
                Assert.Equal("bottom", made.Last(window => window.Screen == wide).Row?["edge"]);
                Assert.Same(otherWideWindow, Assert.Single(made, window => window.Screen == otherWide));
                Assert.Same(tallWindow, Assert.Single(made, window => window.Screen == tall));
                Assert.False(otherWideWindow.Disposed);
                Assert.False(tallWindow.Disposed);
            }
            finally
            {
                foreach (var screen in (ScreenInfo[])[wide, otherWide, tall])
                    shell.Platform.Screens.Remove(screen);
                File.Delete(path);
            }

            return true;
        });
    }

    private static Func<ScreenInfo, IReadOnlyDictionary<string, object?>?, IDisposable> Record(List<Window> made)
    {
        return (screen, row) =>
        {
            var window = new Window(screen, row);
            made.Add(window);
            return window;
        };
    }

    private static string Rows(string name, string edge)
    {
        return $"""
            [{name}]

            [[{name}.screen]]
            manufacturer = "TST"
            model = "Wide"
            serial = "1"
            edge = "{edge}"

            [[{name}.screen]]
            manufacturer = "TST"
            model = "Tall"
            serial = "1"
            edge = "right"

            """;
    }

    internal static ScreenInfo Screen(string manufacturer, string model, string serial)
    {
        var name = "TEST" + Guid.NewGuid().ToString("N");
        return new ScreenInfo(@"\\.\" + name, name, model, manufacturer, serial, new Rect(0, 0, 1920, 1080),
            new Rect(0, 0, 1920, 1040), 1, 0, 0, 60, false);
    }

    private sealed partial class Section(string name) : Preferences(name);

    private sealed class Window(ScreenInfo screen, IReadOnlyDictionary<string, object?>? row) : IShellPart
    {
        public ScreenInfo Screen { get; } = screen;

        public IReadOnlyDictionary<string, object?>? Row { get; } = row;

        public bool Disposed { get; private set; }

        public int Starts { get; private set; }

        public void Start()
        {
            Starts++;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
