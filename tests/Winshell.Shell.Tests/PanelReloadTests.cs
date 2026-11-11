using System.Reflection;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

[Trait("Category", "Window")]
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

        public NestedRoot(UIElement child)
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_edited_control_on_a_surface_is_made_again_in_its_place(bool besideOthers)
    {
        var (keptSurface, keptOther, disposed, old, made) = await ShellFixture.On(async shell =>
        {
            var edited = new EditedWidget();
            var other = new TextBlock { Text = "kept" };
            var row = new StackPanel { Children = { other } };
            if (besideOthers)
                row.Children.Add(edited);
            using var surface = new SurfaceProps { Content = XamlHost(() => besideOthers ? row : edited) };
            var host = surface.Show();
            using var panel = Panel(host);
            await ShellFixture.LaidOut(edited).WaitAsync(TestContext.Current.CancellationToken);

            PanelReload.Apply(shell, [typeof(EditedWidget)]);

            return (ReferenceEquals(panel.Content, host), ReferenceEquals(row.Children[0], other), edited.Disposed,
                edited, besideOthers ? row.Children[1] : Find<EditedWidget>(host));
        });

        Assert.True(keptSurface, "The window keeps the surface a composition filled.");
        if (besideOthers)
            Assert.True(keptOther, "The panel keeps the controls next to the edited one.");
        Assert.True(disposed);
        Assert.NotSame(old, Assert.IsType<EditedWidget>(made));
    }

    [Fact]
    public async Task An_edited_widget_in_a_view_is_made_again_and_the_view_stays()
    {
        var (keptView, disposed, old, made) = await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            Widgets.Register<EditedHost>(name);
            using var area = new WidgetAreaProps { Defaults = [name] };
            var host = area.Show();
            var view = new NestedRoot(host);
            using var panel = Panel(view);
            await Wait.UntilAsync(() => Search<EditedWidget>(host) is not null, "the edited widget to render");
            var widget = Find<EditedWidget>(host);
            await ShellFixture.LaidOut(widget).WaitAsync(TestContext.Current.CancellationToken);

            PanelReload.Apply(shell, [typeof(EditedWidget)]);

            // Read before the area is disposed, which disposes its widgets.
            await Wait.UntilAsync(() => Search<EditedWidget>(host) is { } next && !ReferenceEquals(next, widget),
                "the area to make the edited widget again");
            return (ReferenceEquals(panel.Content, view), widget.Disposed, widget, Find<EditedWidget>(host));
        });

        Assert.True(keptView, "The view keeps its other state, and its area makes the widget again.");
        Assert.True(disposed);
        Assert.NotSame(old, Assert.IsType<EditedWidget>(made));
    }

    [Fact]
    public async Task An_edited_widget_in_a_bar_is_made_again_and_the_bar_keeps_the_rest()
    {
        var (before, after) = await ShellFixture.On(async shell =>
        {
            var edited = Guid.NewGuid().ToString();
            Widgets.Register<EditedHost>(edited);
            var unchanged = Guid.NewGuid().ToString();
            Widgets.Register<UnchangedHost>(unchanged);
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
                await Wait.UntilAsync(() => Shows(window).Count == 2, "both placements to render");
                var before = Shows(window);
                await ShellFixture.LaidOut((FrameworkElement)before[1]).WaitAsync(TestContext.Current.CancellationToken);

                PanelReload.Apply(shell, [typeof(EditedWidget)]);

                Assert.True(ReferenceEquals(window.Content, content), "The bar keeps its surface and its areas.");
                await Wait.UntilAsync(() => Shows(window) is [_, var edited] && !ReferenceEquals(edited, before[1]),
                    "the bar to make the edited widget again");
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

        // The unchanged text and the edited widget, in that order.
        static List<UIElement> Shows(PanelWindow bar)
        {
            var found = new List<UIElement>();
            Walk(bar.Content!);
            var text = found.OfType<TextBlock>().Where(block => block.Text == "unchanged").ToList();
            var edited = found.OfType<EditedWidget>().ToList();
            return text.Count == 1 && edited.Count == 1 ? [text[0], edited[0]] : [];

            void Walk(DependencyObject node)
            {
                if (node is UIElement element)
                    found.Add(element);
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                    Walk(VisualTreeHelper.GetChild(node, i));
            }
        }
    }

    private static T Find<T>(DependencyObject root)
        where T : DependencyObject =>
        Search<T>(root) ?? throw new InvalidOperationException($"No {typeof(T).Name} in the tree.");

    private static T? Search<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T found)
            return found;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (Search<T>(VisualTreeHelper.GetChild(root, i)) is { } child)
                return child;
        return null;
    }

    /// <summary>Renders <see cref="EditedWidget"/>, and disposes it when the area drops the placement.</summary>
    private sealed class EditedHost : Component<WidgetContext>
    {
        public override Element Render()
        {
            EditedWidget? widget = null;
            UseEffect(() => () => widget?.Dispose(), false);
            return XamlHost(() => widget = new EditedWidget());
        }
    }

    /// <summary>The placement a reload must leave alone.</summary>
    private sealed class UnchangedHost : Component<WidgetContext>
    {
        public override Element Render() => XamlHost(() => new TextBlock { Text = "unchanged" });
    }
}
