using Windows.Foundation;

namespace Winshell.ShellTests;

[TestClass]
public sealed class ScreenWindowsTests
{
    [TestMethod]
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
                using var windows = new ScreenWindows("windows" + Guid.NewGuid().ToString("N"), Record(made));
                var firstWindow = Assert.ContainsSingle(window => window.Screen == first, made);

                screens.Add(second);
                var secondWindow = Assert.ContainsSingle(window => window.Screen == second, made);

                var resized = first with { Geometry = new Rect(0, 0, 2560, 1440), DevicePixelRatio = 1.5 };
                screens[screens.IndexOf(first)] = resized;
                screens.Remove(second);
                Assert.IsTrue(secondWindow.Disposed);
                Assert.IsFalse(firstWindow.Disposed);
                Assert.AreSequenceEqual([firstWindow, secondWindow], made.Where(window => window.Screen.Id == first.Id || window.Screen.Id == second.Id));

                var other = resized with { Model = "Other", SerialNumber = "3" };
                screens[screens.IndexOf(resized)] = other;
                Assert.IsTrue(firstWindow.Disposed);
                Assert.ContainsSingle(window => window.Screen == other, made);

                windows.Dispose();
                foreach (var window in made)
                    Assert.IsTrue(window.Disposed);
            }
            finally
            {
                foreach (var screen in screens.Where(screen => screen.Id == first.Id || screen.Id == second.Id).ToArray())
                    screens.Remove(screen);
            }

            return true;
        });
    }

    [TestMethod]
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
                using var windows = new ScreenWindows(name, Record(made));
                var wideWindow = Assert.ContainsSingle(window => window.Screen == wide, made);
                var otherWideWindow = Assert.ContainsSingle(window => window.Screen == otherWide, made);
                var tallWindow = Assert.ContainsSingle(window => window.Screen == tall, made);
                Assert.AreEqual("left", wideWindow.Row?["edge"]);
                Assert.IsNull(otherWideWindow.Row);
                Assert.AreEqual("right", tallWindow.Row?["edge"]);

                File.WriteAllText(path, Rows(name, "bottom"));
                new Section(name).Dispose();
                await Wait.UntilAsync(() =>
                {
                    Assert.IsTrue(shell.Screens.Contains(wide), "The platform refreshed the screens and dropped the made-up ones.");
                    return made.Count(window => window.Screen == wide) == 2;
                }, "the changed row to replace its screen's window");
                Assert.IsTrue(wideWindow.Disposed);
                Assert.AreEqual("bottom", made.Last(window => window.Screen == wide).Row?["edge"]);
                Assert.AreSame(otherWideWindow, Assert.ContainsSingle(window => window.Screen == otherWide, made));
                Assert.AreSame(tallWindow, Assert.ContainsSingle(window => window.Screen == tall, made));
                Assert.IsFalse(otherWideWindow.Disposed);
                Assert.IsFalse(tallWindow.Disposed);
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

    private sealed class Window(ScreenInfo screen, IReadOnlyDictionary<string, object?>? row) : IDisposable
    {
        public ScreenInfo Screen { get; } = screen;

        public IReadOnlyDictionary<string, object?>? Row { get; } = row;

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
