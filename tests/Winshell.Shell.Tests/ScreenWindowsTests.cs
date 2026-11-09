using Windows.Foundation;

namespace Winshell.ShellTests;

/// <summary>
/// One window on each screen for a component: made as a screen appears, disposed as it goes, and made again when the
/// screen's row in the component's section changes. The screens are the shell's own list, with screens added and
/// removed as the platform does when a monitor is connected or removed.
/// </summary>
public sealed class ScreenWindowsTests
{
    [Fact]
    public async Task A_screen_that_appears_gets_a_window_and_one_that_goes_takes_only_its_own()
    {
        await ShellFixture.On(async shell =>
        {
            var first = Screen("TST", "First", "1");
            var second = Screen("TST", "Second", "2");
            var made = new List<Window>();
            shell.Platform.Screens.Add(first);
            try
            {
                var windows = new ScreenWindows("windows" + Guid.NewGuid().ToString("N"), (screen, row) =>
                {
                    var window = new Window(screen, row);
                    made.Add(window);
                    return window;
                });
                var firstWindow = Assert.Single(made, window => window.Screen == first);

                shell.Platform.Screens.Add(second);
                await NextTurn(shell);
                var secondWindow = Assert.Single(made, window => window.Screen == second);
                Assert.Single(made, window => window.Screen == first);

                shell.Platform.Screens.Remove(second);
                await NextTurn(shell);
                Assert.True(secondWindow.Disposed);
                Assert.False(firstWindow.Disposed);
                Assert.Single(made, window => window.Screen == first);

                windows.Dispose();
                Assert.All(made, window => Assert.True(window.Disposed));
            }
            finally
            {
                shell.Platform.Screens.Remove(first);
                shell.Platform.Screens.Remove(second);
            }

            return true;
        });
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
                using var windows = new ScreenWindows(name, (screen, row) =>
                {
                    var window = new Window(screen, row);
                    made.Add(window);
                    return window;
                });
                var wideWindow = Assert.Single(made, window => window.Screen == wide);
                var otherWideWindow = Assert.Single(made, window => window.Screen == otherWide);
                var tallWindow = Assert.Single(made, window => window.Screen == tall);
                Assert.Equal("left", wideWindow.Row?["edge"]);
                Assert.Null(otherWideWindow.Row);
                Assert.Equal("right", tallWindow.Row?["edge"]);

                File.WriteAllText(path, Rows(name, "bottom"));
                await Wait.UntilAsync(() => made.Count(window => window.Screen == wide) == 2,
                    "the changed row to replace its screen's window");
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

    /// <summary>A section with a row for the wide screen with serial 1, on <paramref name="edge"/>, and one for the tall screen.</summary>
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

    /// <summary>A screen with an id no monitor has, as the platform would report a monitor that was connected.</summary>
    private static ScreenInfo Screen(string manufacturer, string model, string serial)
    {
        var name = "TEST" + Guid.NewGuid().ToString("N");
        return new ScreenInfo(@"\\.\" + name, name, model, manufacturer, serial, new Rect(0, 0, 1920, 1080),
            new Rect(0, 0, 1920, 1040), 1, 0, 0, 60, false);
    }

    /// <summary>Completes after the work the shell's thread has queued so far, which holds the windows' next follow.</summary>
    private static Task NextTurn(Shell shell)
    {
        var turn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        shell.Loop.Post(turn.SetResult);
        return turn.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>What a configuration's function made for one screen, and whether it has been disposed.</summary>
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
