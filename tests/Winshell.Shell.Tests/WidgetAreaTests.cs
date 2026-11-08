using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Winshell.ShellTests;

public sealed class WidgetAreaTests
{
    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public async Task Placements_are_arranged_in_order_with_their_own_inputs(Orientation orientation)
    {
        await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            var inputs = new List<WidgetContext>();
            var views = new List<TextBlock>();
            Widgets.Register(name, context =>
            {
                inputs.Add(context);
                var view = new TextBlock { Text = (string)context.Settings["text"], Width = 60, Height = 30 };
                views.Add(view);
                return view;
            });
            var first = new Dictionary<string, object> { ["text"] = "first" };
            var second = new Dictionary<string, object> { ["text"] = "second" };
            using var area = new WidgetArea();
            Assert.Equal(Orientation.Horizontal, area.Orientation);
            area.Orientation = orientation;
            area.Defaults = [new(name, first), new(name, second)];
            using var window = new PanelWindow { Width = 180, Height = 90, Content = area, Visible = true };
            await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);

            Assert.Equal(["first", "second"], views.Select(view => view.Text));
            Assert.All(inputs, context =>
            {
                Assert.Same(shell, context.Shell);
                Assert.Equal(orientation, context.Orientation);
            });
            Assert.Same(first, inputs[0].Settings);
            Assert.Same(second, inputs[1].Settings);
            var start = views[0].TransformToVisual(area).TransformPoint(new Point());
            var next = views[1].TransformToVisual(area).TransformPoint(new Point());
            Assert.Equal(orientation == Orientation.Horizontal ? new Point(60, 0) : new Point(0, 30),
                new Point(next.X - start.X, next.Y - start.Y));
            return true;
        });
    }

    [Theory]
    [InlineData("unknown", "No widget is registered")]
    [InlineData("unsupported", "Needs a horizontal area")]
    [InlineData("throwing", "Creation failed")]
    [InlineData("null", "returned null")]
    public async Task A_creation_failure_names_the_widget_and_reason_between_surviving_placements(string failure, string reason)
    {
        await ShellFixture.On(async _ =>
        {
            var good = Guid.NewGuid().ToString();
            var bad = Guid.NewGuid().ToString();
            Widgets.Register(good, context =>
            {
                Assert.Empty(context.Settings);
                return new TextBlock { Text = "survivor" };
            });
            if (failure != "unknown")
                Widgets.Register(bad, _ => failure switch
                {
                    "unsupported" => throw new UnsupportedWidgetException("Needs a horizontal area"),
                    "throwing" => throw new InvalidOperationException("Creation failed"),
                    _ => null!
                });
            using var area = new WidgetArea { Defaults = [new(good), new(bad), new(good)] };
            using var window = new PanelWindow { Width = 600, Height = 60, Content = area, Visible = true };
            await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);

            var text = TextIn(area).ToArray();
            Assert.Equal(3, text.Length);
            Assert.Equal("survivor", text[0]);
            Assert.Contains(bad, text[1], StringComparison.Ordinal);
            Assert.Contains(reason, text[1], StringComparison.Ordinal);
            Assert.Equal("survivor", text[2]);
            return true;
        });
    }

    [Fact]
    public async Task Widgets_survive_reparenting_and_are_disposed_on_rebuild_and_explicit_disposal()
    {
        await ShellFixture.On(async _ =>
        {
            var name = Guid.NewGuid().ToString();
            var views = new List<DisposableWidget>();
            Widgets.Register(name, _ =>
            {
                var view = new DisposableWidget();
                views.Add(view);
                return view;
            });
            using var area = new WidgetArea { Defaults = [new(name), new(name)] };
            using var window = new PanelWindow { Width = 180, Height = 90, Content = area, Visible = true };
            await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);
            var unloaded = new TaskCompletionSource();
            area.Unloaded += (_, _) => unloaded.TrySetResult();
            window.Content = new Grid();
            await unloaded.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.All(views, view => Assert.Equal(0, view.Disposals));
            window.Content = area;

            area.Defaults = [new(name)];
            Assert.Equal([1, 1, 0], views.Select(view => view.Disposals));
            area.Orientation = Orientation.Vertical;
            Assert.Equal([1, 1, 1, 0], views.Select(view => view.Disposals));
            area.Dispose();
            area.Dispose();
            Assert.All(views, view => Assert.Equal(1, view.Disposals));
            Assert.Empty(TextIn(area));
            Assert.Throws<ObjectDisposedException>(() => area.Defaults = [new(name)]);
            return true;
        });
    }

    [Fact]
    public async Task A_failed_disposal_still_disposes_the_remaining_widgets_once()
    {
        await ShellFixture.On(_ =>
        {
            var bad = Guid.NewGuid().ToString();
            var good = Guid.NewGuid().ToString();
            var first = new DisposableWidget { FailDisposal = true };
            var second = new DisposableWidget();
            Widgets.Register(bad, _ => first);
            Widgets.Register(good, _ => second);
            using var area = new WidgetArea { Defaults = [new(bad), new(good)] };

            Assert.Throws<InvalidOperationException>(area.Dispose);
            area.Dispose();
            Assert.Equal(1, first.Disposals);
            Assert.Equal(1, second.Disposals);
            return true;
        });
    }

    [Fact]
    public async Task The_theme_loads_from_its_uri_and_leaves_other_resources_owned_by_the_area()
    {
        await ShellFixture.On(_ =>
        {
            using var area = new WidgetArea();
            var theme = new Uri("ms-appx:///WidgetTheme.xaml");
            area.Resources["author"] = "keep";
            area.Theme = theme;
            Assert.Equal(theme, area.Theme);
            Assert.Equal("widget theme", area.Resources["WidgetThemeMessage"]);
            area.Theme = null;
            Assert.False(area.Resources.ContainsKey("WidgetThemeMessage"));
            Assert.Equal("keep", area.Resources["author"]);
            return true;
        });
    }

    private static IEnumerable<string> TextIn(DependencyObject root)
    {
        if (root is TextBlock text)
            yield return text.Text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in TextIn(VisualTreeHelper.GetChild(root, i)))
                yield return child;
    }

    private sealed partial class DisposableWidget : UserControl, IDisposable
    {
        public DisposableWidget()
        {
            Content = new TextBlock { Text = "widget" };
        }

        public int Disposals { get; private set; }
        public bool FailDisposal { get; init; }

        public void Dispose()
        {
            Disposals++;
            if (FailDisposal)
                throw new InvalidOperationException("Disposal failed");
        }
    }
}
