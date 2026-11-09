using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winshell.ShellTests;

public sealed partial class PanelReloadTests
{
    private sealed partial class Edited : Grid
    {
        private static int Built { get; set; }

        public Edited()
        {
            Built++;
        }

        // The compiler closure in this method is the edit the closure test reloads.
        public Func<int> Counter()
        {
            var seen = Built;
            return () => seen;
        }
    }

    private sealed partial class Throws : Grid
    {
        public static bool Fail { get; set; }

        public Throws()
        {
            if (Fail)
                throw new InvalidOperationException("half typed");
        }
    }

    private sealed partial class Takes(int size) : Grid
    {
        // Nothing reads this. The constructor argument is what stops a reload from replacing the content.
        public int Size { get; } = size;
    }

    private sealed partial class Disposable : Grid, IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed partial class EditedWidget : Grid, IDisposable
    {
        public bool Disposed { get; private set; }

        public EditedWidget()
        {
            Width = 20;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private static PanelWindow Panel(UIElement content)
    {
        return new PanelWindow
        {
            Anchors = Edges.Top | Edges.Left,
            Width = 200,
            Height = 40,
            Content = content,
            Visible = true
        };
    }

    [Fact]
    public async Task An_edited_panel_gets_new_content_and_the_others_keep_theirs()
    {
        var (replaced, same, kept) = await ShellFixture.On(shell =>
        {
            var edited = new Edited();
            var other = new Grid();
            using var editedPanel = Panel(edited);
            using var otherPanel = Panel(other);
            var handle = editedPanel.Host!.Handle;

            PanelReload.Apply(shell, [typeof(Edited)]);

            return (editedPanel.Content is Edited fresh && !ReferenceEquals(fresh, edited),
                editedPanel.Host!.Handle == handle, ReferenceEquals(otherPanel.Content, other));
        });

        Assert.True(replaced);
        Assert.True(same, "The window stays. Only its content is new.");
        Assert.True(kept);
    }

    [Fact]
    public async Task An_edit_to_a_closure_counts_as_an_edit_to_its_type()
    {
        var replaced = await ShellFixture.On(shell =>
        {
            var edited = new Edited();
            using var panel = Panel(edited);

            var closure = typeof(Edited).GetNestedTypes(BindingFlags.NonPublic)
                .Single(type => type.Name.StartsWith('<'));
            PanelReload.Apply(shell, [closure]);

            return !ReferenceEquals(panel.Content, edited);
        });

        Assert.True(replaced);
    }

    [Fact]
    public async Task A_panel_that_shows_an_edited_control_gets_new_content()
    {
        var built = await ShellFixture.On(async shell =>
        {
            var inner = new Edited();
            var root = new NestedRoot(inner);
            using var panel = Panel(root);
            await ShellFixture.LaidOut(inner);

            PanelReload.Apply(shell, [typeof(Edited)]);

            return !ReferenceEquals(panel.Content, root);
        });

        Assert.True(built);
    }

    private sealed partial class NestedRoot : Grid
    {
        // PanelReload rebuilds the panel by invoking this constructor through reflection.
        public NestedRoot()
        {
        }

        public NestedRoot(Edited child)
        {
            Children.Add(child);
        }
    }

    [Fact]
    public async Task Content_that_throws_while_it_is_typed_stays_as_it_was()
    {
        var kept = await ShellFixture.On(shell =>
        {
            var content = new Throws();
            using var panel = Panel(content);
            Throws.Fail = true;
            try
            {
                PanelReload.Apply(shell, [typeof(Throws)]);
            }
            finally
            {
                Throws.Fail = false;
            }

            return ReferenceEquals(panel.Content, content);
        });

        Assert.True(kept);
    }

    [Fact]
    public async Task Content_that_needs_arguments_stays_as_it_was()
    {
        var kept = await ShellFixture.On(shell =>
        {
            var content = new Takes(3);
            using var panel = Panel(content);

            PanelReload.Apply(shell, [typeof(Takes)]);

            return ReferenceEquals(panel.Content, content);
        });

        Assert.True(kept);
    }

    [Fact]
    public async Task The_content_it_replaces_is_disposed()
    {
        var disposed = await ShellFixture.On(shell =>
        {
            var content = new Disposable();
            using var panel = Panel(content);

            PanelReload.Apply(shell, [typeof(Disposable)]);

            return content.Disposed;
        });

        Assert.True(disposed);
    }

    [Fact]
    public async Task An_edited_widget_in_a_bar_is_made_again_and_the_bar_keeps_the_rest()
    {
        var (before, after) = await ShellFixture.On(async shell =>
        {
            var edited = Guid.NewGuid().ToString();
            Widgets.Register(edited, _ => new EditedWidget());
            var unchanged = Guid.NewGuid().ToString();
            Widgets.Register(unchanged, _ => new TextBlock { Text = "unchanged" });
            var screen = ScreenWindowsTests.Screen("TST", "Reload", "1");
            shell.Platform.Screens.Add(screen);
            try
            {
                using var bar = new Bar
                {
                    Name = "bar" + Guid.NewGuid().ToString("N"),
                    Reserve = false,
                    Start = [unchanged],
                    Center = [edited]
                };
                var shown = new TaskCompletionSource();
                shell.Loop.Post(shown.SetResult);
                await shown.Task.WaitAsync(TestContext.Current.CancellationToken);
                var window = shell.Platform.Windows.Select(host => host.Client).OfType<PanelWindow>()
                    .Single(panel => panel.Screen == screen);
                var content = window.Content;
                var before = Shows(window);
                await ShellFixture.LaidOut((FrameworkElement)before[1]).WaitAsync(TestContext.Current.CancellationToken);

                PanelReload.Apply(shell, [typeof(EditedWidget)]);

                Assert.True(ReferenceEquals(window.Content, content), "The bar keeps its surface and its areas.");
                return (before, Shows(window));
            }
            finally
            {
                shell.Platform.Screens.Remove(screen);
            }
        });

        Assert.Equal(2, after.Count);
        Assert.Same(before[0], after[0]);
        Assert.True(Assert.IsType<EditedWidget>(before[1]).Disposed);
        Assert.NotSame(before[1], Assert.IsType<EditedWidget>(after[1]));

        // The widgets in the bar's areas, in order.
        static List<UIElement> Shows(PanelWindow bar)
        {
            return
            [
                .. Assert.IsType<Grid>(Assert.IsType<Surface>(bar.Content).Content).Children.Cast<WidgetArea>()
                    .SelectMany(area => Assert.IsAssignableFrom<Panel>(area.Content).Children)
            ];
        }
    }
}
