using System.Globalization;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

public sealed class WidgetAreaTests
{
    [Theory]
    [Trait("Category", "Window")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Popup_content_follows_its_area_until_the_widget_is_removed(bool disposeArea)
    {
        await ShellFixture.On(async _ =>
        {
            var name = Guid.NewGuid().ToString();
            PopupCapture.Context = null;
            Widgets.Register<PopupCapture>(name);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Style = TestStyles.Marked(),
                Defaults = [new(name)]
            });
            var host = area.Host;
            using var window = new PanelWindow { Width = 180, Height = 90, Content = host, Visible = true };
            await ShellFixture.LaidOut(host).WaitAsync(TestContext.Current.CancellationToken);
            var input = PopupCapture.Context!;
            var content = new StackPanel { Children = { new TextBlock { Text = "popup" } } };
            var text = (TextBlock)content.Children[0];
            text.Resources["widget-owned"] = "keep";
            var root = input!.StylePopup(content);
            var second = input.StylePopup(new TextBlock { Text = "second popup" });
            using var popup = new PopupWindow { Rect = new Rect(200, 100, 200, 90), Content = root, Visible = true };
            using var another = new PopupWindow { Rect = new Rect(410, 100, 200, 90), Content = second, Visible = true };
            await ShellFixture.LaidOut(text).WaitAsync(TestContext.Current.CancellationToken);

            Assert.Equal(36d, root.Resources["ShellSlotSize"]);
            Assert.Equal(36d, second.Resources["ShellSlotSize"]);
            Assert.Equal("keep", text.Resources["widget-owned"]);

            area.Set(area.Props with { Style = null });
            await host.WaitForIdleAsync();
            Assert.Equal(32d, root.Resources["ShellSlotSize"]);
            area.Set(area.Props with { Style = TestStyles.Marked() });
            await host.WaitForIdleAsync();
            Assert.Equal(36d, root.Resources["ShellSlotSize"]);

            if (disposeArea)
                area.Dispose();
            else
            {
                area.Set(area.Props with { Defaults = [] });
                await host.WaitForIdleAsync();
            }

            if (!disposeArea)
            {
                area.Set(area.Props with { Style = null });
                await host.WaitForIdleAsync();
            }

            Assert.Equal("keep", text.Resources["widget-owned"]);
            Assert.Equal(36d, root.Resources["ShellSlotSize"]);
            Assert.Throws<ObjectDisposedException>(() => input.StylePopup(new TextBlock()));
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_popup_takes_the_area_style_before_the_area_is_in_a_host_and_keeps_it_once_shown()
    {
        await ShellFixture.On(async _ =>
        {
            var name = Guid.NewGuid().ToString();
            PopupCapture.Context = null;
            Widgets.Register<PopupCapture>(name);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Style = TestStyles.Marked(),
                Defaults = [new(name)]
            });
            await Wait.UntilAsync(() => PopupCapture.Context is not null,
                "the widget to render before the area is in a host");
            var content = new TextBlock { Text = "popup" };
            var root = PopupCapture.Context!.StylePopup(content);
            Assert.Equal(36d, root.Resources["ShellSlotSize"]);

            var host = area.Host;
            using var window = new PanelWindow { Width = 180, Height = 90, Content = host, Visible = true };
            using var popup = new PopupWindow { Rect = new Rect(200, 100, 200, 90), Content = root, Visible = true };
            await ShellFixture.LaidOut(host).WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(36d, root.Resources["ShellSlotSize"]);
            return true;
        });
    }

    [Theory]
    [Trait("Category", "Window")]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public async Task Placements_are_arranged_in_order_with_their_own_inputs(Orientation orientation)
    {
        await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            OrderedWidget.Inputs = [];
            OrderedWidget.Views = [];
            Widgets.Register<OrderedWidget>(name);
            var first = new Dictionary<string, object> { ["text"] = "first" };
            var second = new Dictionary<string, object> { ["text"] = "second" };
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps());
            Assert.Equal(Orientation.Horizontal, area.Props.Orientation);
            area.Set(area.Props with { Orientation = orientation });
            area.Set(area.Props with { Defaults = [new(name, first), new(name, second)] });
            var host = area.Host;
            using var window = new PanelWindow { Width = 180, Height = 90, Content = host, Visible = true };
            await ShellFixture.LaidOut(host).WaitAsync(TestContext.Current.CancellationToken);
            var views = OrderedWidget.Views;
            var inputs = OrderedWidget.Inputs;
            Assert.Equal(["first", "second"], views.Select(view => view.Text));
            Assert.All(inputs, context =>
            {
                Assert.Same(shell, context.Shell);
                Assert.Equal(orientation, context.Orientation);
            });
            Assert.Same(first, inputs[0].Settings);
            Assert.Same(second, inputs[1].Settings);
            var start = views[0].TransformToVisual(host).TransformPoint(new Point());
            var next = views[1].TransformToVisual(host).TransformPoint(new Point());
            Assert.Equal(orientation == Orientation.Horizontal ? new Point(60, 0) : new Point(0, 30),
                new Point(next.X - start.X, next.Y - start.Y));
            return true;
        });
    }

    [Theory]
    [Trait("Category", "Window")]
    [InlineData("unknown", "No widget is registered", "Could not create")]
    [InlineData("unsupported", "Needs a horizontal area", "Could not render")]
    [InlineData("throwing", "Creation failed", "Could not render")]
    public async Task A_creation_failure_names_the_widget_and_reason_between_surviving_placements(
        string failure, string reason, string logged)
    {
        await ShellFixture.On(async shell =>
        {
            var good = Guid.NewGuid().ToString();
            var bad = Guid.NewGuid().ToString();
            Widgets.Register<SurvivorWidget>(good);
            BadWidget.Failure = failure;
            if (failure != "unknown")
                Widgets.Register<BadWidget>(bad);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Defaults = [new(good), new(bad), new(good)] });
            using var window = new PanelWindow { Width = 280, Height = 60, Content = area.Host, Visible = true };
            string[] text = [];
            await Wait.UntilAsync(() =>
            {
                text = [.. TextIn(area.Host)];
                return text.Length >= 3;
            }, "the area to show the failure between the survivors");

            Assert.Equal(3, text.Length);
            Assert.Equal("survivor", text[0]);
            Assert.Contains(bad, text[1], StringComparison.Ordinal);
            Assert.Contains(reason, text[1], StringComparison.Ordinal);
            Assert.Equal("survivor", text[2]);
            var log = ShellFixture.ReadLog(shell);
            var entry = $"ERROR widgets: {logged} {bad}";
            Assert.Contains(entry, log, StringComparison.Ordinal);
            log = log[log.IndexOf(entry, StringComparison.Ordinal)..];
            Assert.Contains(reason, log, StringComparison.Ordinal);
            Assert.Contains("\n   at ", log, StringComparison.Ordinal);
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_named_area_takes_its_widgets_from_its_section_live()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = "area" + Guid.NewGuid().ToString("N");
            var unknown = "missing" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            TrackedWidget.Mounts = [];
            Widgets.Register<TrackedWidget>(widget);
            File.WriteAllText(path, $"[{name}]\nwidgets = [\n  {{ widget = '{widget}', text = 'first', count = 3, options = {{ enabled = true }} }},\n" +
                                    $"  {{ widget = '{widget}', text = 'second' }},\n]\n\n" +
                                    $"[{unknown}]\nwidgets = [{{ widget = 'not-installed' }}]\n");
            try
            {
                using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Name = name, Defaults = [new(widget)] });
                using var other = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Name = name + "other", Defaults = [new(widget)] });
                using var window = new PanelWindow
                {
                    Width = 240,
                    Height = 80,
                    Content = new StackPanel { Children = { area.Host, other.Host } },
                    Visible = true
                };
                await Wait.UntilAsync(() => TrackedWidget.Mounts.Count >= 3, "the placements to mount");
                var created = TrackedWidget.Mounts;
                var placed = created.Where(item => item.Context.Settings.ContainsKey("text")).ToArray();
                Assert.Equal(["first", "second"], placed.Select(item => item.Context.Settings["text"]));
                Assert.Equal(3L, placed[0].Context.Settings["count"]);
                Assert.Equal(true, Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
                    placed[0].Context.Settings["options"])["enabled"]);
                var unchanged = Assert.Single(created, item => item.Context.Settings.Count == 0);
                await Wait.UntilAsync(() => ShellFixture.ReadLog(shell).Contains($"no component named '{unknown}'", StringComparison.Ordinal),
                    "the warning for the section no component reads");
                Assert.Contains($"[{unknown}]", File.ReadAllText(path), StringComparison.Ordinal);

                var before = created.Count;
                // Replacing the file also exercises editors that save by rename.
                var next = path + ".next";
                File.WriteAllText(next, $"[{name}]\nwidgets = [{{ widget = '{widget}', text = 'updated' }}]\n");
                File.Move(next, path, true);
                await Wait.UntilAsync(() => created.Count > before, "the saved list to rebuild");
                Assert.All(placed, item => Assert.Equal(1, item.Unmounts));
                Assert.Equal("updated", created[^1].Context.Settings["text"]);
                Assert.Equal(0, unchanged.Unmounts);
                var updated = created[^1];

                before = created.Count;
                var anotherUnknown = unknown + "again";
                File.WriteAllText(path, $"# A comment and an unrelated section leave the existing widgets alone.\n" +
                                        $"[{name}]\nwidgets = [{{ widget = '{widget}', text = 'updated' }}]\n" +
                                        $"[{anotherUnknown}]\n");
                await Wait.UntilAsync(() => ShellFixture.ReadLog(shell).Contains($"no component named '{anotherUnknown}'", StringComparison.Ordinal),
                    "the equivalent list to be read");
                Assert.Equal(before, created.Count);
                Assert.Equal(0, updated.Unmounts);

                File.WriteAllText(path, $"[{name}]\nwidgets = []\n");
                await Wait.UntilAsync(() => updated.Unmounts == 1, "the empty list to remove the widget");
                Assert.Empty(TextIn(area.Host));
                Assert.Equal(0, unchanged.Unmounts);

                before = created.Count;
                File.WriteAllText(path, $"# The area is absent, so it uses the author's defaults again.\n");
                await Wait.UntilAsync(() => created.Count > before, "the absent section to restore the defaults");
                Assert.Empty(created[^1].Context.Settings);
                Assert.Equal(0, unchanged.Unmounts);
            }
            finally
            {
                File.Delete(path);
            }
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task Renaming_an_area_reads_the_section_of_its_new_name()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = "area" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            Widgets.Register<TextWidget>(widget);
            File.WriteAllText(path, $"[{name}side]\nwidgets = [{{ widget = '{widget}', text = 'side' }}]\n");
            try
            {
                using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Name = name, Defaults = [new(widget)] });
                using var window = new PanelWindow { Width = 160, Height = 40, Content = area.Host, Visible = true };
                await Wait.UntilAsync(() => TextIn(area.Host).SequenceEqual(["default"]), "the author's default");

                area.Set(area.Props with { Name = name + "side" });
                await Wait.UntilAsync(() => TextIn(area.Host).SequenceEqual(["side"]), "the section of the new name");

                area.Set(area.Props with { Name = "" });
                await Wait.UntilAsync(() => TextIn(area.Host).SequenceEqual(["default"]), "an unnamed area to use its defaults");
            }
            finally
            {
                File.Delete(path);
            }
            return true;
        });
    }

    [Theory]
    [Trait("Category", "Window")]
    [InlineData("[{0}\n", "[1:")]
    [InlineData("[{0}]\nwidgets = [{{ widget = 42 }}]\n", "[2:1]: widgets must name a widget in each of its tables")]
    [InlineData("{0} = []\n", "[1:1]: {0} should be a section")]
    public async Task A_broken_edit_keeps_the_last_valid_list(string broken, string diagnostic)
    {
        await ShellFixture.On(async shell =>
        {
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = "area" + Guid.NewGuid().ToString("N");
            broken = string.Format(CultureInfo.InvariantCulture, broken, name);
            diagnostic = string.Format(CultureInfo.InvariantCulture, diagnostic, name);
            var widget = Guid.NewGuid().ToString();
            TrackedWidget.Mounts = [];
            Widgets.Register<TrackedWidget>(widget);
            File.WriteAllText(path, $"[{name}]\nwidgets = [{{ widget = '{widget}' }}]\n");
            try
            {
                using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Name = name });
                using var window = new PanelWindow { Width = 160, Height = 40, Content = area.Host, Visible = true };
                await Wait.UntilAsync(() => TrackedWidget.Mounts.Count == 1, "the placement to mount");
                var views = TrackedWidget.Mounts;
                Assert.Single(views);
                var logLength = ShellFixture.ReadLog(shell).Length;
                File.WriteAllText(path, broken);
                await Wait.UntilAsync(() => ShellFixture.ReadLog(shell)[logLength..].Contains(diagnostic, StringComparison.Ordinal),
                    "the broken edit to be diagnosed with its location");
                Assert.Equal(broken, File.ReadAllText(path));
                Assert.Equal(0, Assert.Single(views).Unmounts);
                await Wait.UntilAsync(() => TextIn(area.Host).Count() == 1, "the widget to stay on screen");

                File.WriteAllText(path, $"[{name}]\nwidgets = []\n");
                await Wait.UntilAsync(() => views[0].Unmounts == 1, "a valid edit after the broken one to apply");
                Assert.Empty(TextIn(area.Host));
            }
            finally
            {
                File.Delete(path);
            }
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task Widgets_survive_reparenting_and_are_disposed_on_rebuild_and_explicit_disposal()
    {
        await ShellFixture.On(async _ =>
        {
            var name = Guid.NewGuid().ToString();
            MountedWidget.Mounted = 0;
            Widgets.Register<MountedWidget>(name);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Defaults = [new(name), new(name)] });
            var host = area.Host;
            using var window = new PanelWindow { Width = 180, Height = 90, Content = host, Visible = true };
            await ShellFixture.LaidOut(host).WaitAsync(TestContext.Current.CancellationToken);
            await Wait.UntilAsync(() => MountedWidget.Mounted == 2, "both placements to mount");
            var unloaded = new TaskCompletionSource();
            host.Unloaded += (_, _) => unloaded.TrySetResult();
            window.Content = new Grid();
            await unloaded.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, MountedWidget.Mounted);
            window.Content = host;

            area.Set(area.Props with { Defaults = [new(name)] });
            await Wait.UntilAsync(() => MountedWidget.Mounted == 1, "the removed placement to unmount");
            // The placement that stays is the same component. A_new_orientation_or_defaults_renders_the_same_component
            // proves it renders the new orientation. The host unmounts the components; the area does not dispose them.
            area.Set(area.Props with { Orientation = Orientation.Vertical });
            Assert.Equal(1, MountedWidget.Mounted);
            area.Dispose();
            area.Dispose();
            Assert.Equal(0, MountedWidget.Mounted);
            Assert.Empty(TextIn(host));
            // Props are not disposable, so a later change is not an ObjectDisposedException. Disposing the host
            // again is a no-op, and the widgets are already gone.
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_style_replaces_its_dictionary_and_leaves_the_area_its_own_resources()
    {
        await ShellFixture.On(async shell =>
        {
            var controls = new PlainControls();
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Defaults = [new(controls.Button), new(controls.Text)]
            });
            var marked = TestStyles.Marked();
            var other = TestStyles.Other();
            using var window = new PanelWindow { Width = 220, Height = 80, Content = area.Host, Visible = true };
            await Wait.UntilAsync(() => controls.Buttons.Count == 1 && controls.Lines.Count == 1, "the controls to render");
            var (button, _) = Assert.Single(controls.Buttons);
            var text = Assert.Single(controls.Lines);
            await ShellFixture.LaidOut(text).WaitAsync(TestContext.Current.CancellationToken);
            var shown = AreaShowing(text);
            shown.Resources["author"] = "keep";
            // Merged after the style, so this slot size wins for the controls in the area.
            shown.Resources.MergedDictionaries.Add(new ResourceDictionary { ["ShellSlotSize"] = 50.0 });
            try
            {
                area.Set(area.Props with { Style = marked });
                await Wait.UntilAsync(
                    () => text.FontFamily.Source == "Georgia" && button.ActualWidth == 50 && button.ActualHeight == 50,
                    "the text to take Georgia and the button the author's slot");
                Assert.Equal(marked, area.Props.Style);
                Assert.Equal(Microsoft.UI.Colors.White, ColorOf(text.Foreground));
                Assert.Equal((50d, 50d), (button.ActualWidth, button.ActualHeight));
                shown = AreaShowing(text);
                Assert.Equal("keep", shown.Resources["author"]);
                Assert.Equal(50.0, shown.Resources["ShellSlotSize"]);

                shell.Style = other;
                await area.Host.WaitForIdleAsync();
                Assert.Equal("Georgia", text.FontFamily.Source);
                Assert.Equal(Microsoft.UI.Colors.White, ColorOf(text.Foreground));
                Assert.Equal((50d, 50d), (button.ActualWidth, button.ActualHeight));

                area.Set(area.Props with { Style = null });
                await Wait.UntilAsync(() => text.FontFamily.Source == "Consolas", "the area to follow the shell style");
                Assert.Equal(Microsoft.UI.Colors.Crimson, ColorOf(text.Foreground));
                Assert.Equal(other, shell.Style);
                Assert.Equal((50d, 50d), (button.ActualWidth, button.ActualHeight));

                shell.Style = null;
                await Wait.UntilAsync(
                    () => text.FontFamily.Source == "Segoe UI Variable Text,Segoe UI",
                    "the area to return to the built-in style");
                shown = AreaShowing(text);
                Assert.Equal("keep", shown.Resources["author"]);
                Assert.Equal(50.0, shown.Resources["ShellSlotSize"]);
                Assert.Equal((50d, 50d), (button.ActualWidth, button.ActualHeight));
            }
            finally
            {
                shell.Style = null;
            }

            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_custom_style_keeps_its_colors_font_and_slot_in_light_and_dark()
    {
        await ShellFixture.On(async _ =>
        {
            var controls = new PlainControls();
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Style = TestStyles.Other(),
                Defaults = [new(controls.Button), new(controls.Text)]
            });
            var host = area.Host;
            using var window = new PanelWindow { Width = 220, Height = 80, Content = host, Visible = true };
            await Wait.UntilAsync(() => controls.Buttons.Count == 1 && controls.Lines.Count == 1, "the controls to render");
            var (button, _) = Assert.Single(controls.Buttons);
            var text = Assert.Single(controls.Lines);
            await ShellFixture.LaidOut(text).WaitAsync(TestContext.Current.CancellationToken);

            foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark })
            {
                host.RequestedTheme = theme;
                await Wait.UntilAsync(
                    () => ColorOf(text.Foreground) == Microsoft.UI.Colors.Crimson
                        && button.ActualWidth == 40 && button.ActualHeight == 40,
                    "the text to keep the style color");
                Assert.Equal(Microsoft.UI.Colors.Crimson, ColorOf(button.Foreground));
                Assert.Equal(("Consolas", 18.0), (text.FontFamily.Source, text.FontSize));
                Assert.Equal((40d, 40d), (button.ActualWidth, button.ActualHeight));
            }

            area.Set(area.Props with { Style = null });
            host.RequestedTheme = ElementTheme.Light;
            await Wait.UntilAsync(() =>
                ColorOf(text.Foreground) == WindowsText("Light") && button.ActualWidth == 32,
                "the built-in light text color and slot");
            Assert.Equal(WindowsText("Light"), ColorOf(button.Foreground));
            Assert.Equal(("Segoe UI Variable Text,Segoe UI", 14.0), (text.FontFamily.Source, text.FontSize));

            host.RequestedTheme = ElementTheme.Dark;
            await Wait.UntilAsync(
                () => ColorOf(text.Foreground) == WindowsText("Default") && button.ActualWidth == 32 && button.ActualHeight == 32,
                "the built-in dark text color");
            Assert.Equal(WindowsText("Default"), ColorOf(button.Foreground));
            Assert.Equal((32d, 32d), (button.ActualWidth, button.ActualHeight));
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task With_no_style_plain_controls_take_the_built_in_style_and_follow_the_windows_theme()
    {
        await ShellFixture.On(async _ =>
        {
            var controls = new PlainControls();
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Defaults = [new(controls.Button), new(controls.Text)]
            });
            var host = area.Host;
            host.RequestedTheme = ElementTheme.Light;
            using var window = new PanelWindow { Width = 200, Height = 60, Content = host, Visible = true };
            await ShellFixture.LaidOut(host).WaitAsync(TestContext.Current.CancellationToken);

            var (glyphButton, glyph) = Assert.Single(controls.Buttons);
            var text = Assert.Single(controls.Lines);
            Assert.Equal((32, 32), (glyphButton.ActualWidth, glyphButton.ActualHeight));
            Assert.Equal("Segoe Fluent Icons,Segoe MDL2 Assets", glyph.FontFamily.Source);
            Assert.Equal("Segoe UI Variable Text,Segoe UI", text.FontFamily.Source);
            Assert.Equal(14, text.FontSize);
            Assert.Equal(WindowsText("Light"), ColorOf(text.Foreground));
            Assert.Equal(WindowsText("Light"), ColorOf(glyphButton.Foreground));

            host.RequestedTheme = ElementTheme.Dark;
            await Wait.UntilAsync(() => ColorOf(text.Foreground) == WindowsText("Default"),
                "the text to take Windows' dark text color");
            Assert.Equal(WindowsText("Default"), ColorOf(glyphButton.Foreground));
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task The_shell_style_reaches_areas_and_popups_live_and_an_area_style_wins_over_it()
    {
        await ShellFixture.On(async shell =>
        {
            var other = TestStyles.Other();
            var controls = new PlainControls();
            var popupWidget = Guid.NewGuid().ToString();
            PopupCapture.Context = null;
            Widgets.Register<PopupCapture>(popupWidget);
            using var follows = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Defaults = [new(controls.Button), new(controls.Text), new(popupWidget)]
            });
            using var own = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Style = TestStyles.Marked(),
                Defaults = [new(controls.Text)]
            });
            using var window = new PanelWindow
            {
                Width = 400,
                Height = 60,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { follows.Host, own.Host } },
                Visible = true
            };
            await Wait.UntilAsync(
                () => controls.Buttons.Count == 1 && controls.Lines.Count == 2 && PopupCapture.Context is not null,
                "the placements to render");
            var (glyphButton, glyph) = Assert.Single(controls.Buttons);
            var text = controls.Lines[0];
            var ownText = controls.Lines[1];
            var popupText = new TextBlock { Text = "popup" };
            using var popup = new PopupWindow
            {
                Rect = new Rect(200, 100, 200, 90),
                Content = PopupCapture.Context!.StylePopup(popupText),
                Visible = true
            };
            try
            {
                await ShellFixture.LaidOut(popupText).WaitAsync(TestContext.Current.CancellationToken);
                Assert.Equal("Segoe UI Variable Text,Segoe UI", popupText.FontFamily.Source);
                var ownFont = ownText.FontFamily.Source;

                shell.Style = other;
                await Wait.UntilAsync(() => glyphButton.ActualWidth == 40 && glyphButton.ActualHeight == 40,
                    "the glyph button to take the other style's slot size");
                Assert.Equal(("Consolas", 18.0), (text.FontFamily.Source, text.FontSize));
                Assert.Equal(Microsoft.UI.Colors.Crimson, ColorOf(text.Foreground));
                Assert.Equal(Microsoft.UI.Colors.Crimson, ColorOf(glyphButton.Foreground));
                Assert.Equal("Segoe Fluent Icons,Segoe MDL2 Assets", glyph.FontFamily.Source);
                Assert.Equal(16, glyph.FontSize);
                Assert.Equal(("Consolas", 18.0), (popupText.FontFamily.Source, popupText.FontSize));
                Assert.Equal("Georgia", ownText.FontFamily.Source);
                Assert.Equal(ownFont, ownText.FontFamily.Source);

                shell.Style = null;
                await Wait.UntilAsync(() => glyphButton.ActualWidth == 32, "the area to return to the built-in style");
                Assert.Equal("Segoe UI Variable Text,Segoe UI", text.FontFamily.Source);
                Assert.Equal("Segoe UI Variable Text,Segoe UI", popupText.FontFamily.Source);
                Assert.Equal("Georgia", ownText.FontFamily.Source);
            }
            finally
            {
                shell.Style = null;
            }
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_tooltip_takes_the_shell_style_live_not_its_area_style_and_a_configuration_tooltip_style_wins()
    {
        await ShellFixture.On(async shell =>
        {
            var widget = Guid.NewGuid().ToString();
            var tip = new ToolTip { Content = "tip" };
            TipWidget.Tip = tip;
            Widgets.Register<TipWidget>(widget);
            var other = TestStyles.Other();
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Style = other, Defaults = [new(widget)] });
            using var window = new PanelWindow { Width = 100, Height = 40, Content = area.Host, Visible = true };
            await ShellFixture.LaidOut(area.Host).WaitAsync(TestContext.Current.CancellationToken);

            TaskCompletionSource? toggled = null;
            tip.Opened += (_, _) => toggled?.TrySetResult();
            tip.Closed += (_, _) => toggled?.TrySetResult();
            async Task Toggle(bool open)
            {
                toggled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                tip.IsOpen = open;
                await toggled.Task.WaitAsync(TestContext.Current.CancellationToken);
            }

            async Task<(global::Windows.UI.Color Background, global::Windows.UI.Color Foreground, string Font)> Open()
            {
                await Toggle(true);
                var look = (ColorOf(tip.Background), ColorOf(tip.Foreground), tip.FontFamily.Source);
                await Toggle(false);
                return look;
            }

            try
            {
                (string, ElementTheme)[] themes = [("Light", ElementTheme.Light), ("Default", ElementTheme.Dark)];
                foreach (var (theme, requested) in themes)
                {
                    tip.RequestedTheme = requested;
                    Assert.Equal((WindowsColor(theme, "SolidBackgroundFillColorBase"),
                        WindowsColor(theme, "TextFillColorPrimary"), "Segoe UI Variable Text,Segoe UI"), await Open());
                }

                shell.Style = other;
                Assert.Equal((global::Windows.UI.Color.FromArgb(0xff, 0x1a, 0x1b, 0x26), Microsoft.UI.Colors.Crimson,
                    "Consolas"), await Open());

                var own = new Style(typeof(ToolTip));
                own.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Microsoft.UI.Colors.Gold)));
                Application.Current.Resources[typeof(ToolTip)] = own;
                shell.Style = null;
                Assert.Equal(Microsoft.UI.Colors.Gold, (await Open()).Background);
            }
            finally
            {
                Application.Current.Resources.Remove(typeof(ToolTip));
                shell.Style = null;
            }
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_widget_reads_the_alert_color_from_the_style()
    {
        await ShellFixture.On(async _ =>
        {
            var name = Guid.NewGuid().ToString();
            AlertWidget.Alert = null;
            Widgets.Register<AlertWidget>(name);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Style = TestStyles.Other(), Defaults = [new(name)] });
            using var window = new PanelWindow { Width = 120, Height = 40, Content = area.Host, Visible = true };
            await Wait.UntilAsync(() => AlertWidget.Alert is not null, "the alert text to render");
            var alert = AlertWidget.Alert!;
            await ShellFixture.LaidOut(alert).WaitAsync(TestContext.Current.CancellationToken);

            Assert.Equal(Microsoft.UI.Colors.Orange, ColorOf(alert!.Foreground));
            return true;
        });
    }

    private static global::Windows.UI.Color ColorOf(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    /// <summary>The area that shows <paramref name="control"/>: the outermost stack that lays its placements out.</summary>
    private static StackPanel AreaShowing(FrameworkElement control)
    {
        StackPanel? area = null;
        for (DependencyObject? node = control; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is StackPanel panel)
                area = panel;
        Assert.NotNull(area);
        return area;
    }

    /// <summary>The text color Windows gives a theme: "Light", or "Default" for dark.</summary>
    private static global::Windows.UI.Color WindowsText(string theme) => WindowsColor(theme, "TextFillColorPrimary");

    /// <summary>A color Windows gives a theme: "Light", or "Default" for dark.</summary>
    internal static global::Windows.UI.Color WindowsColor(string theme, string key)
    {
        var value = WindowsResource(theme, key);
        return value is SolidColorBrush brush ? brush.Color : (global::Windows.UI.Color)value;
    }

    private static object WindowsResource(string theme, string key)
    {
        var controls = Application.Current.Resources.MergedDictionaries.OfType<XamlControlsResources>().Single();
        return ((ResourceDictionary)controls.ThemeDictionaries[theme])[key];
    }

    private static IEnumerable<string> TextIn(DependencyObject root)
    {
        if (root is TextBlock text)
            yield return text.Text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in TextIn(VisualTreeHelper.GetChild(root, i)))
                yield return child;
    }

    /// <summary>Two widgets of plain controls, a button holding a glyph and a line of text, and what each placement made.</summary>
    private sealed class PlainControls
    {
        public PlainControls()
        {
            Current = this;
            Widgets.Register<GlyphWidget>(Button);
            Widgets.Register<LineWidget>(Text);
        }

        public static PlainControls Current { get; private set; } = null!;

        public string Button { get; } = Guid.NewGuid().ToString();
        public string Text { get; } = Guid.NewGuid().ToString();
        public List<(Button Button, FontIcon Glyph)> Buttons { get; } = [];
        public List<TextBlock> Lines { get; } = [];
    }

    /// <summary>Keeps the first props, so a later render with the shell window does not replace the popup styling.</summary>
    private sealed class PopupCapture : Component<WidgetContext>
    {
        public static WidgetContext? Context;

        public override Element Render()
        {
            Context ??= Props;
            return XamlHost(() => new TextBlock { Text = "widget" });
        }
    }

    /// <summary>A text block of a fixed size, so a test can measure how an area arranges placements.</summary>
    private sealed class OrderedWidget : Component<WidgetContext>
    {
        public static List<WidgetContext> Inputs = [];
        public static List<TextBlock> Views = [];

        public override Element Render() => XamlHost(() =>
        {
            Inputs.Add(Props);
            var view = new TextBlock { Text = (string)Props.Settings["text"], Width = 60, Height = 30 };
            Views.Add(view);
            return view;
        });
    }

    /// <summary>The placement that keeps running beside one that fails.</summary>
    private sealed class SurvivorWidget : Component<WidgetContext>
    {
        public override Element Render()
        {
            Assert.Empty(Props.Settings);
            return TextBlock("survivor");
        }
    }

    /// <summary>Fails while rendering, which the area shows in that placement.</summary>
    private sealed class BadWidget : Component<WidgetContext>
    {
        public static string Failure = "";

        public override Element Render() => Failure switch
        {
            "unsupported" => throw new UnsupportedWidgetException("Needs a horizontal area"),
            _ => throw new InvalidOperationException("Creation failed")
        };
    }

    /// <summary>One mount of a placement, and how many times the area has unmounted it.</summary>
    internal sealed class Mount(WidgetContext context)
    {
        public WidgetContext Context { get; } = context;

        public int Unmounts { get; private set; }

        public void Unmount() => Unmounts++;
    }

    /// <summary>Records each mount, so a test can see a placement leave without a control of its own.</summary>
    private sealed class TrackedWidget : Component<WidgetContext>
    {
        public static List<Mount> Mounts = [];

        public override Element Render()
        {
            var mount = UseRef<Mount?>(null);
            if (mount.Current is null)
            {
                mount.Current = new Mount(Props);
                Mounts.Add(mount.Current);
            }

            var mounted = mount.Current;
            UseEffect(() => mounted.Unmount, false);
            var text = Props.Settings.TryGetValue("text", out var value) ? (string)value : "widget";
            return TextBlock(text);
        }
    }

    /// <summary>Shows its <c>text</c> setting, or <c>default</c> when the placement has none.</summary>
    private sealed class TextWidget : Component<WidgetContext>
    {
        public override Element Render()
        {
            var text = Props.Settings.TryGetValue("text", out var value) ? (string)value : "default";
            return TextBlock(text);
        }
    }

    /// <summary>Stays mounted while its area is moved, and leaves when the area drops it.</summary>
    private sealed class MountedWidget : Component<WidgetContext>
    {
        public static int Mounted;

        public override Element Render()
        {
            UseEffect(() =>
            {
                Mounted++;
                return () => Mounted--;
            }, false);
            return TextBlock("widget");
        }
    }

    private sealed class GlyphWidget : Component<WidgetContext>
    {
        public override Element Render() => XamlHost(() =>
        {
            var glyph = new FontIcon { Glyph = "" };
            var button = new Button { Content = glyph };
            PlainControls.Current.Buttons.Add((button, glyph));
            return button;
        });
    }

    private sealed class LineWidget : Component<WidgetContext>
    {
        public override Element Render() => XamlHost(() =>
        {
            var line = new TextBlock { Text = "12:00" };
            PlainControls.Current.Lines.Add(line);
            return line;
        });
    }

    private sealed class TipWidget : Component<WidgetContext>
    {
        public static ToolTip? Tip;

        public override Element Render() => XamlHost(() =>
        {
            var button = new Button { Content = "B" };
            ToolTipService.SetToolTip(button, Tip);
            return button;
        });
    }

    private sealed class AlertWidget : Component<WidgetContext>
    {
        public static TextBlock? Alert;

        public override Element Render() => XamlHost(() =>
        {
            Alert = new TextBlock
            {
                Text = "alert",
                Foreground = new SolidColorBrush(Props.Style!.Alert)
            };
            return Alert;
        });
    }
}
