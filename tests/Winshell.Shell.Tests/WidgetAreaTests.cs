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
        await ShellFixture.On(async shell =>
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
            var log = ReadLog(shell);
            var entry = $"ERROR widgets: Could not create {bad}";
            Assert.Contains(entry, log, StringComparison.Ordinal);
            log = log[log.IndexOf(entry, StringComparison.Ordinal)..];
            Assert.Contains(reason, log, StringComparison.Ordinal);
            Assert.Contains("\n   at ", log, StringComparison.Ordinal);
            return true;
        });
    }

    [Fact]
    public async Task The_file_replaces_named_lists_live_and_keeps_unmatched_entries()
    {
        await ShellFixture.On(async shell =>
        {
            var path = Path.Combine(shell.ConfigRoot, "widgets", shell.ShellId + ".toml");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = "area" + Guid.NewGuid().ToString("N");
            var unknown = "missing" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            var created = new List<(DisposableWidget View, WidgetContext Context)>();
            Widgets.Register(widget, context =>
            {
                var view = new DisposableWidget();
                created.Add((view, context));
                return view;
            });
            File.WriteAllText(path, $"[[{name}]]\nwidget = '{widget}'\ntext = 'first'\ncount = 3\noptions = {{ enabled = true }}\n" +
                                    $"[[{name}]]\nwidget = '{widget}'\ntext = 'second'\n" +
                                    $"[[{unknown}]]\nwidget = 'not-installed'\n");
            try
            {
                using var area = new WidgetArea { Name = name, Defaults = [new(widget)] };
                using var other = new WidgetArea { Name = name + "other", Defaults = [new(widget)] };
                await Wait.UntilAsync(() => created.Count(item => item.Context.Settings.ContainsKey("text")) == 2,
                    "the file's two placements to replace the named area's default");
                var placed = created.Where(item => item.Context.Settings.ContainsKey("text")).ToArray();
                Assert.Equal(["first", "second"], placed.Select(item => item.Context.Settings["text"]));
                Assert.Equal(3L, placed[0].Context.Settings["count"]);
                Assert.Equal(true, Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
                    placed[0].Context.Settings["options"])["enabled"]);
                var unchanged = created.Last(item => item.Context.Settings.Count == 0).View;
                await Wait.UntilAsync(() => ReadLog(shell).Contains($"no widget area named '{unknown}'", StringComparison.Ordinal),
                    "the warning for the unmatched area");
                Assert.Contains($"[[{unknown}]]", File.ReadAllText(path), StringComparison.Ordinal);

                var before = created.Count;
                // Replacing the file also exercises editors that save by rename.
                var next = path + ".next";
                File.WriteAllText(next, $"[[{name}]]\nwidget = '{widget}'\ntext = 'updated'\n");
                File.Move(next, path, true);
                await Wait.UntilAsync(() => created.Count > before, "the saved list to rebuild");
                Assert.All(placed, item => Assert.Equal(1, item.View.Disposals));
                Assert.Equal("updated", created[^1].Context.Settings["text"]);
                Assert.Equal(0, unchanged.Disposals);
                var updated = created[^1].View;

                before = created.Count;
                var anotherUnknown = unknown + "again";
                File.WriteAllText(path, $"# A comment and an unrelated area leave the existing widgets alone.\n" +
                                        $"[[{name}]]\nwidget = '{widget}'\ntext = 'updated'\n" +
                                        $"[[{anotherUnknown}]]\nwidget = 'not-installed'\n");
                await Wait.UntilAsync(() => ReadLog(shell).Contains($"no widget area named '{anotherUnknown}'", StringComparison.Ordinal),
                    "the equivalent list to be read");
                Assert.Equal(before, created.Count);
                Assert.Equal(0, updated.Disposals);

                File.WriteAllText(path, $"{name} = []\n");
                await Wait.UntilAsync(() => updated.Disposals == 1, "the empty list to remove the widget");
                Assert.Empty(TextIn(area));
                Assert.Equal(0, unchanged.Disposals);

                before = created.Count;
                File.WriteAllText(path, "# Use the author's defaults again.\n");
                await Wait.UntilAsync(() => created.Count > before, "removing the entry to restore defaults");
                Assert.Empty(created[^1].Context.Settings);
                Assert.Equal(0, unchanged.Disposals);
            }
            finally
            {
                File.Delete(path);
            }
            return true;
        });
    }

    [Theory]
    [InlineData("[[broken]\n", "[1:")]
    [InlineData("broken = [{ widget = 42 }]\n", "must be a list of tables")]
    public async Task A_broken_edit_keeps_the_last_valid_lists(string broken, string diagnostic)
    {
        await ShellFixture.On(async shell =>
        {
            var path = Path.Combine(shell.ConfigRoot, "widgets", shell.ShellId + ".toml");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = "area" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            var views = new List<DisposableWidget>();
            Widgets.Register(widget, _ =>
            {
                var view = new DisposableWidget();
                views.Add(view);
                return view;
            });
            File.WriteAllText(path, $"[[{name}]]\nwidget = '{widget}'\n");
            try
            {
                using var area = new WidgetArea { Name = name };
                await Wait.UntilAsync(() => views.Count == 1, "the last valid placement to load");
                var logLength = ReadLog(shell).Length;
                File.WriteAllText(path, broken);
                await Wait.UntilAsync(() => ReadLog(shell)[logLength..].Contains(diagnostic, StringComparison.Ordinal),
                    "the broken edit to be diagnosed with its location");
                Assert.Equal(broken, File.ReadAllText(path));
                Assert.Equal(0, Assert.Single(views).Disposals);
                Assert.Single(TextIn(area));

                File.WriteAllText(path, $"{name} = []\n");
                await Wait.UntilAsync(() => views[0].Disposals == 1, "a valid edit after the broken one to apply");
                Assert.Empty(TextIn(area));
            }
            finally
            {
                File.Delete(path);
            }
            return true;
        });
    }

    [Fact]
    public async Task A_missing_file_starts_with_commented_defaults_for_each_named_area()
    {
        await ShellFixture.On(async shell =>
        {
            var path = Path.Combine(shell.ConfigRoot, "widgets", shell.ShellId + ".toml");
            File.Delete(path);
            var name = "area" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            Widgets.Register(widget, _ => new TextBlock { Text = "default" });
            try
            {
                using var area = new WidgetArea
                {
                    Name = name,
                    Defaults = [new(widget, new Dictionary<string, object>
                    {
                        ["text"] = "a \"quoted\" value\nand a line break",
                        ["count"] = 7,
                        ["nested"] = new Dictionary<string, object> { ["enabled"] = true }
                    }), new(widget)]
                };
                using var empty = new WidgetArea { Name = name + "empty" };
                await Wait.UntilAsync(() => File.Exists(path), "the initial placement file to be written");
                var text = File.ReadAllText(path);
                Assert.All(text.Split('\n').Where(line => line.Length > 0), line => Assert.StartsWith("#", line));
                var uncommented = string.Join('\n', text.Split('\n').SkipWhile(line => line.Length > 0)
                    .Select(line => line.StartsWith("# ", StringComparison.Ordinal) ? line[2..] : line));
                Assert.True(TomlDocument.TryParse(path, uncommented, out var document, out var error), error);
                var rows = Assert.IsAssignableFrom<IReadOnlyList<object>>(document.Values[name]);
                Assert.Equal(2, rows.Count);
                var first = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(rows[0]);
                Assert.Equal(widget, first["widget"]);
                Assert.Equal("a \"quoted\" value\nand a line break", first["text"]);
                Assert.Equal(7L, first["count"]);
                Assert.Equal(true, Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(first["nested"])["enabled"]);
                Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<object>>(document.Values[name + "empty"]));
                Assert.Equal(["default", "default"], TextIn(area));
            }
            finally
            {
                File.Delete(path);
            }
            return true;
        });
    }

    private static string ReadLog(Shell shell)
    {
        using var stream = InstanceLog.OpenToRead(shell.LogPath);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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
