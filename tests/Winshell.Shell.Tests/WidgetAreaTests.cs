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
                Assert.Same(shell.Style, context.Style);
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
                                    $"[{unknown}]\nwidgets = ['not-installed']\n");
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

    [Fact]
    public async Task A_named_area_reads_bare_widget_names_mixed_with_tables_in_order()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = "area" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            TrackedWidget.Mounts = [];
            Widgets.Register<TrackedWidget>(widget);
            File.WriteAllText(path, $"[{name}]\nwidgets = [\"{widget}\", {{ widget = \"{widget}\", text = \"Hi\" }}, \"{widget}\"]\n");
            try
            {
                using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Name = name });
                await Wait.UntilAsync(() => TrackedWidget.Mounts.Count == 3, "the three placements to mount");
                Assert.Equal([0, 1, 0], TrackedWidget.Mounts.Select(mount => mount.Context.Settings.Count));
                Assert.Equal("Hi", TrackedWidget.Mounts[1].Context.Settings["text"]);
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        });
    }

    [Theory]
    [InlineData("42")]
    [InlineData("{ text = \"Hi\" }")]
    [InlineData("\" \"")]
    [InlineData("[\"clock\"]")]
    public async Task A_named_area_refuses_a_list_with_an_item_that_names_no_widget_and_logs_its_line(string item)
    {
        await ShellFixture.On(async shell =>
        {
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = "area" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            TrackedWidget.Mounts = [];
            Widgets.Register<TrackedWidget>(widget);
            var logLength = ShellFixture.ReadLog(shell).Length;
            File.WriteAllText(path, $"[{name}]\nwidgets = [\"{widget}\", {item}]\n");
            try
            {
                using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
                {
                    Name = name,
                    Defaults = [new(widget, new Dictionary<string, object> { ["text"] = "default" })]
                });
                await Wait.UntilAsync(() => ShellFixture.ReadLog(shell)[logLength..].Contains(
                        "[2:1]: widgets must be a list of widget names or tables that each name a widget, " +
                        "so it was left unchanged", StringComparison.Ordinal),
                    "the refused list to be logged with its line");
                await Wait.UntilAsync(() => TrackedWidget.Mounts.Count == 1, "the default placement to mount");
                Assert.Equal("default", Assert.Single(TrackedWidget.Mounts).Context.Settings["text"]);
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
    [InlineData("[{0}]\nwidgets = [{{ widget = 42 }}]\n",
        "[2:1]: widgets must be a list of widget names or tables that each name a widget")]
    [InlineData("[{0}]\nwidgets = [\"{1}\", 42]\n",
        "[2:1]: widgets must be a list of widget names or tables that each name a widget")]
    [InlineData("[{0}]\nwidgets = [\"{1}\", {{ text = \"Hi\" }}]\n",
        "[2:1]: widgets must be a list of widget names or tables that each name a widget")]
    [InlineData("{0} = []\n", "[1:1]: {0} should be a section")]
    public async Task A_broken_edit_keeps_the_last_valid_list(string broken, string diagnostic)
    {
        await ShellFixture.On(async shell =>
        {
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = "area" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            broken = string.Format(CultureInfo.InvariantCulture, broken, name, widget);
            diagnostic = string.Format(CultureInfo.InvariantCulture, diagnostic, name);
            TrackedWidget.Mounts = [];
            Widgets.Register<TrackedWidget>(widget);
            File.WriteAllText(path, $"[{name}]\nwidgets = [\"{widget}\"]\n");
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
            area.Set(area.Props with { Orientation = Orientation.Vertical });
            Assert.Equal(1, MountedWidget.Mounted);
            area.Dispose();
            area.Dispose();
            Assert.Equal(0, MountedWidget.Mounted);
            Assert.Empty(TextIn(host));
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

                shell.Style = new();
                await Wait.UntilAsync(
                    () => text.FontFamily.Source == "Segoe UI Variable Text,Segoe UI",
                    "the area to return to the default style");
                shown = AreaShowing(text);
                Assert.Equal("keep", shown.Resources["author"]);
                Assert.Equal(50.0, shown.Resources["ShellSlotSize"]);
                Assert.Equal((50d, 50d), (button.ActualWidth, button.ActualHeight));
            }
            finally
            {
                shell.Style = new();
            }

            return true;
        });
    }

    [Fact]
    public async Task A_contrast_theme_paints_with_its_own_colors_for_a_custom_style_and_the_default_one()
    {
        await ShellFixture.On(_ =>
        {
            var other = TestStyles.Other();
            foreach (var style in new[] { other, new ShellStyle() })
            {
                var contrast = (ResourceDictionary)Shell.LoadStyle(style).ThemeDictionaries["HighContrast"];
                Assert.NotEqual(other.Background, ColorOf((Brush)contrast["ShellBackgroundBrush"]));
                Assert.NotEqual(other.Foreground, ColorOf((Brush)contrast["ShellForegroundBrush"]));
                Assert.NotEqual(other.Alert, ColorOf((Brush)contrast["ShellAlertBrush"]));
            }

            return true;
        });
    }

    [Fact]
    public async Task A_style_that_sets_only_the_font_size_takes_windows_system_colors_in_a_contrast_theme()
    {
        await ShellFixture.On(_ =>
        {
            var style = Shell.LoadStyle(new ShellStyle { FontSize = 12 });
            Assert.Equal(12.0, style["ShellFontSize"]);
            Assert.Equal(
                (WindowsColor("HighContrast", "SystemColorWindowColor"), WindowsColor("HighContrast", "SystemColorWindowTextColor"),
                    WindowsColor("HighContrast", "SystemColorHighlightColor")),
                StyleColors(style, "HighContrast"));
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task An_alert_takes_windows_critical_color_in_light_and_dark_and_follows_a_switch_while_mounted()
    {
        await ShellFixture.On(async shell =>
        {
            var controls = new AlertControls();
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Defaults = [new(controls.Name)] });
            var host = area.Host;
            host.RequestedTheme = ElementTheme.Light;
            using var window = new PanelWindow { Width = 240, Height = 60, Content = host, Visible = true };
            await Wait.UntilAsync(() => controls.Button is not null && controls.Text is not null && controls.Plain is not null,
                "the alert controls to render");
            var (button, text, plain) = (controls.Button!, controls.Text!, controls.Plain!);
            await ShellFixture.LaidOut(text).WaitAsync(TestContext.Current.CancellationToken);

            if (shell.Platform.IsHighContrast)
            {
                var highlight = WindowsColor("HighContrast", "SystemColorHighlightColor");
                Assert.Equal((highlight, highlight), (ColorOf(text.Foreground), ColorOf(button.Foreground)));
                Assert.Equal(WindowsColor("HighContrast", "SystemColorWindowTextColor"), ColorOf(plain.Foreground));
                return true;
            }

            foreach (var (requested, theme) in new[] { (ElementTheme.Light, "Light"), (ElementTheme.Dark, "Default"), (ElementTheme.Light, "Light") })
            {
                host.RequestedTheme = requested;
                var critical = WindowsColor(theme, "SystemFillColorCritical");
                await Wait.UntilAsync(() => ColorOf(text.Foreground) == critical, "the alert to take Windows' critical color");
                Assert.Equal(critical, ColorOf(button.Foreground));
                Assert.Equal(WindowsText(theme), ColorOf(plain.Foreground));
                Assert.Equal(("Segoe UI Variable Text,Segoe UI", 14.0), (text.FontFamily.Source, text.FontSize));
                Assert.Equal((32d, 32d), (button.ActualWidth, button.ActualHeight));
            }

            foreach (var style in new[] { new ShellStyle { FontSize = 12 }, new ShellStyle { Foreground = Microsoft.UI.Colors.Crimson } })
            {
                area.Set(area.Props with { Style = style });
                foreach (var (requested, theme) in new[] { (ElementTheme.Light, "Light"), (ElementTheme.Dark, "Default") })
                {
                    host.RequestedTheme = requested;
                    var critical = WindowsColor(theme, "SystemFillColorCritical");
                    var foreground = style.Foreground ?? WindowsText(theme);
                    await Wait.UntilAsync(
                        () => ColorOf(text.Foreground) == critical && ColorOf(plain.Foreground) == foreground && text.FontSize == style.FontSize,
                        "an unset alert to keep Windows' critical color under the area's style");
                    Assert.Equal(critical, ColorOf(button.Foreground));
                }
            }

            area.Set(area.Props with { Style = TestStyles.Other() });
            foreach (var theme in new[] { ElementTheme.Dark, ElementTheme.Light })
            {
                host.RequestedTheme = theme;
                await Wait.UntilAsync(
                    () => ColorOf(text.Foreground) == Microsoft.UI.Colors.Orange && button.ActualWidth == 40,
                    "the alert to take the area style's alert color");
                Assert.Equal(Microsoft.UI.Colors.Orange, ColorOf(button.Foreground));
                Assert.Equal(Microsoft.UI.Colors.Crimson, ColorOf(plain.Foreground));
                Assert.Equal(("Consolas", 18.0), (text.FontFamily.Source, text.FontSize));
                Assert.Equal((40d, 40d), (button.ActualWidth, button.ActualHeight));
            }

            return true;
        });
    }

    [Fact]
    public async Task The_shell_style_starts_as_windows_colors_and_never_becomes_null()
    {
        await ShellFixture.On(shell =>
        {
            Assert.Equal((null, null, null, 14.0, 32.0),
                (shell.Style.Background, shell.Style.Foreground, shell.Style.Alert, shell.Style.FontSize, shell.Style.SlotSize));
            var before = shell.Style;
            Assert.Throws<ArgumentNullException>(() => shell.Style = null!);
            Assert.Same(before, shell.Style);
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_style_that_sets_only_the_font_size_shows_windows_text_colors_in_light_and_dark()
    {
        await ShellFixture.On(async _ =>
        {
            var controls = new PlainControls();
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
            {
                Style = new ShellStyle { FontSize = 12 },
                Defaults = [new(controls.Button), new(controls.Text)]
            });
            var host = area.Host;
            using var window = new PanelWindow { Width = 200, Height = 60, Content = host, Visible = true };
            await Wait.UntilAsync(() => controls.Buttons.Count == 1 && controls.Lines.Count == 1, "the controls to render");
            var (button, _) = Assert.Single(controls.Buttons);
            var text = Assert.Single(controls.Lines);
            await ShellFixture.LaidOut(text).WaitAsync(TestContext.Current.CancellationToken);

            foreach (var (requested, theme) in new[] { (ElementTheme.Light, "Light"), (ElementTheme.Dark, "Default") })
            {
                host.RequestedTheme = requested;
                await Wait.UntilAsync(() => ColorOf(text.Foreground) == WindowsText(theme),
                    "the text to take Windows' text color");
                Assert.Equal(WindowsText(theme), ColorOf(button.Foreground));
                Assert.Equal(("Segoe UI Variable Text,Segoe UI", 12.0), (text.FontFamily.Source, text.FontSize));
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
                ColorOf(text.Foreground) == WindowsText("Light") && button.ActualWidth == 32 && button.ActualHeight == 32,
                "the default light text color and slot");
            Assert.Equal(WindowsText("Light"), ColorOf(button.Foreground));
            Assert.Equal(("Segoe UI Variable Text,Segoe UI", 14.0), (text.FontFamily.Source, text.FontSize));
            Assert.Equal((32d, 32d), (button.ActualWidth, button.ActualHeight));

            host.RequestedTheme = ElementTheme.Dark;
            await Wait.UntilAsync(
                () => ColorOf(text.Foreground) == WindowsText("Default") && button.ActualWidth == 32 && button.ActualHeight == 32,
                "the default dark text color");
            Assert.Equal(WindowsText("Default"), ColorOf(button.Foreground));
            Assert.Equal((32d, 32d), (button.ActualWidth, button.ActualHeight));
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task With_the_default_style_plain_controls_follow_the_windows_theme()
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

                shell.Style = new();
                await Wait.UntilAsync(() => glyphButton.ActualWidth == 32, "the area to return to the default style");
                Assert.Equal("Segoe UI Variable Text,Segoe UI", text.FontFamily.Source);
                Assert.Equal("Segoe UI Variable Text,Segoe UI", popupText.FontFamily.Source);
                Assert.Equal("Georgia", ownText.FontFamily.Source);
            }
            finally
            {
                shell.Style = new();
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
                shell.Style = new();
                Assert.Equal(Microsoft.UI.Colors.Gold, (await Open()).Background);
            }
            finally
            {
                Application.Current.Resources.Remove(typeof(ToolTip));
                shell.Style = new();
            }
            return true;
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_widget_reads_the_alert_color_from_the_style()
    {
        await ShellFixture.On(async shell =>
        {
            var name = Guid.NewGuid().ToString();
            AlertWidget.Alert = null;
            Widgets.Register<AlertWidget>(name);
            using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Style = TestStyles.Other(), Defaults = [new(name)] });
            using var window = new PanelWindow { Width = 120, Height = 40, Content = area.Host, Visible = true };
            await Wait.UntilAsync(() => AlertWidget.Alert is not null, "the alert text to render");
            var alert = AlertWidget.Alert!;
            await ShellFixture.LaidOut(alert).WaitAsync(TestContext.Current.CancellationToken);

            var expected = shell.Platform.IsHighContrast
                ? WindowsColor("HighContrast", "SystemColorHighlightColor")
                : Microsoft.UI.Colors.Orange;
            Assert.Equal(expected, ColorOf(alert.Foreground));
            return true;
        });
    }

    private static global::Windows.UI.Color ColorOf(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    private static (global::Windows.UI.Color Background, global::Windows.UI.Color Foreground, global::Windows.UI.Color Alert)
        StyleColors(ResourceDictionary style, string theme)
    {
        var colors = (ResourceDictionary)style.ThemeDictionaries[theme];
        return (ColorOf((Brush)colors["ShellBackgroundBrush"]), ColorOf((Brush)colors["ShellForegroundBrush"]),
            ColorOf((Brush)colors["ShellAlertBrush"]));
    }

    private static StackPanel AreaShowing(FrameworkElement control)
    {
        StackPanel? area = null;
        for (DependencyObject? node = control; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is StackPanel panel)
                area = panel;
        Assert.NotNull(area);
        return area;
    }

    private static global::Windows.UI.Color WindowsText(string theme) => WindowsColor(theme, "TextFillColorPrimary");

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

    private sealed class PopupCapture : Widget
    {
        public static WidgetContext? Context;

        public override Element Render()
        {
            Context ??= Props;
            return XamlHost(() => new TextBlock { Text = "widget" });
        }
    }

    private sealed class OrderedWidget : Widget
    {
        public static List<WidgetContext> Inputs = [];
        public static List<TextBlock> Views = [];

        public override Element Render() => XamlHost(() =>
        {
            Inputs.Add(Props);
            var view = new TextBlock { Text = (string)Settings["text"], Width = 60, Height = 30 };
            Views.Add(view);
            return view;
        });
    }

    private sealed class SurvivorWidget : Widget
    {
        public override Element Render()
        {
            Assert.Empty(Settings);
            return TextBlock("survivor");
        }
    }

    private sealed class BadWidget : Widget
    {
        public static string Failure = "";

        public override Element Render() => Failure switch
        {
            "unsupported" => throw new UnsupportedWidgetException("Needs a horizontal area"),
            _ => throw new InvalidOperationException("Creation failed")
        };
    }

    internal sealed class Mount(WidgetContext context)
    {
        public WidgetContext Context { get; } = context;

        public int Unmounts { get; private set; }

        public void Unmount() => Unmounts++;
    }

    private sealed class TrackedWidget : Widget
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
            var text = Setting("text", "widget");
            return TextBlock(text);
        }
    }

    private sealed class TextWidget : Widget
    {
        public override Element Render()
        {
            var text = Setting("text", "default");
            return TextBlock(text);
        }
    }

    private sealed class MountedWidget : Widget
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

    private sealed class GlyphWidget : Widget
    {
        public override Element Render() => XamlHost(() =>
        {
            var glyph = new FontIcon { Glyph = "" };
            var button = new Button { Content = glyph };
            PlainControls.Current.Buttons.Add((button, glyph));
            return button;
        });
    }

    private sealed class LineWidget : Widget
    {
        public override Element Render() => XamlHost(() =>
        {
            var line = new TextBlock { Text = "12:00" };
            PlainControls.Current.Lines.Add(line);
            return line;
        });
    }

    private sealed class TipWidget : Widget
    {
        public static ToolTip? Tip;

        public override Element Render() => XamlHost(() =>
        {
            var button = new Button { Content = "B" };
            ToolTipService.SetToolTip(button, Tip);
            return button;
        });
    }

    private sealed class AlertWidget : Widget
    {
        public static TextBlock? Alert;

        public override Element Render() =>
            TextBlock("alert").OnMount(element => Alert = (TextBlock)element).Alert();
    }

    private sealed class AlertControls
    {
        public AlertControls()
        {
            Current = this;
            Widgets.Register<AlertControlsWidget>(Name);
        }

        public static AlertControls Current { get; private set; } = null!;

        public string Name { get; } = Guid.NewGuid().ToString();

        public Button? Button { get; set; }

        public TextBlock? Text { get; set; }

        public TextBlock? Plain { get; set; }
    }

    private sealed class AlertControlsWidget : Widget
    {
        public override Element Render()
        {
            var shown = AlertControls.Current;
            return HStack(
                Button("B").OnMount(element => shown.Button = (Button)element).Alert(),
                TextBlock("alert").OnMount(element => shown.Text = (TextBlock)element).Alert(),
                TextBlock("plain").OnMount(element => shown.Plain = (TextBlock)element).Alert(false));
        }
    }
}
