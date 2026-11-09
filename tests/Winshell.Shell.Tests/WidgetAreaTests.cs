using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Winshell.ShellTests;

public sealed class WidgetAreaTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Popup_content_follows_its_area_until_the_widget_is_removed(bool disposeArea)
    {
        await ShellFixture.On(async _ =>
        {
            var name = Guid.NewGuid().ToString();
            WidgetContext? input = null;
            Widgets.Register(name, context =>
            {
                input = context;
                return new DisposableWidget();
            });
            using var area = new WidgetArea
            {
                Style = new Uri("ms-appx:///WidgetTheme.xaml"),
                RequestedTheme = ElementTheme.Light,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 23,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson),
                Defaults = [new(name)]
            };
            var parent = new Grid { RequestedTheme = ElementTheme.Light, Children = { area } };
            using var window = new PanelWindow { Width = 180, Height = 90, Content = parent, Visible = true };
            await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);
            var content = (StackPanel)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                            Background="{ThemeResource CardBackgroundFillColorDefaultBrush}">
                    <TextBlock Text="popup" />
                </StackPanel>
                """);
            var text = (TextBlock)content.Children[0];
            text.Resources["widget-owned"] = "keep";
            var root = input!.StylePopup(content);
            var second = input.StylePopup(new TextBlock { Text = "second popup" });
            using var popup = new PopupWindow { Rect = new Rect(200, 100, 200, 90), Content = root, Visible = true };
            using var another = new PopupWindow { Rect = new Rect(410, 100, 200, 90), Content = second, Visible = true };
            await ShellFixture.LaidOut(text).WaitAsync(TestContext.Current.CancellationToken);

            Assert.Equal("widget theme", root.Resources["WidgetThemeMessage"]);
            Assert.Equal("widget theme", second.Resources["WidgetThemeMessage"]);
            Assert.Equal("keep", text.Resources["widget-owned"]);
            Assert.Equal(ElementTheme.Light, root.RequestedTheme);
            Assert.Equal(Microsoft.UI.Colors.DarkRed, Assert.IsType<SolidColorBrush>(content.Background).Color);
            Assert.Equal("Consolas", text.FontFamily.Source);
            Assert.Equal(23, text.FontSize);
            Assert.Equal(Microsoft.UI.Colors.Crimson, Assert.IsType<SolidColorBrush>(text.Foreground).Color);

            area.RequestedTheme = ElementTheme.Dark;
            await Wait.UntilAsync(() => root.ActualTheme == ElementTheme.Dark && second.ActualTheme == ElementTheme.Dark,
                "both popup windows to follow the area's dark theme");
            Assert.Equal(ElementTheme.Dark, root.RequestedTheme);
            Assert.Equal(Microsoft.UI.Colors.LightGreen, Assert.IsType<SolidColorBrush>(content.Background).Color);
            area.RequestedTheme = ElementTheme.Default;
            await Wait.UntilAsync(() => root.ActualTheme == ElementTheme.Light,
                "the popup to follow the area's inherited light theme");
            Assert.Equal(ElementTheme.Light, root.RequestedTheme);
            area.RequestedTheme = ElementTheme.Dark;
            await Wait.UntilAsync(() => root.ActualTheme == ElementTheme.Dark, "the popup to return to dark");
            area.FontFamily = new FontFamily("Georgia");
            area.FontSize = 27;
            area.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
            Assert.Equal("Georgia", text.FontFamily.Source);
            Assert.Equal(27, text.FontSize);
            Assert.Equal(Microsoft.UI.Colors.LimeGreen, Assert.IsType<SolidColorBrush>(text.Foreground).Color);
            area.Style = null;
            Assert.False(root.Resources.ContainsKey("WidgetThemeMessage"));
            area.Style = new Uri("ms-appx:///WidgetTheme.xaml");
            Assert.Equal("widget theme", root.Resources["WidgetThemeMessage"]);

            if (disposeArea)
                area.Dispose();
            else
                area.Defaults = [];
            area.RequestedTheme = ElementTheme.Light;
            area.FontFamily = new FontFamily("Arial");
            area.FontSize = 12;
            area.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Blue);
            if (!disposeArea)
                area.Style = null;
            await Wait.UntilAsync(() => area.ActualTheme == ElementTheme.Light, "the area to return to light");
            Assert.Equal(ElementTheme.Dark, root.RequestedTheme);
            Assert.Equal("Georgia", text.FontFamily.Source);
            Assert.Equal(27, text.FontSize);
            Assert.Equal(Microsoft.UI.Colors.LimeGreen, Assert.IsType<SolidColorBrush>(text.Foreground).Color);
            Assert.Equal("widget theme", root.Resources["WidgetThemeMessage"]);
            Assert.Throws<ObjectDisposedException>(() => input.StylePopup(new TextBlock()));
            return true;
        });
    }

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
            var log = ShellFixture.ReadLog(shell);
            var entry = $"ERROR widgets: Could not create {bad}";
            Assert.Contains(entry, log, StringComparison.Ordinal);
            log = log[log.IndexOf(entry, StringComparison.Ordinal)..];
            Assert.Contains(reason, log, StringComparison.Ordinal);
            Assert.Contains("\n   at ", log, StringComparison.Ordinal);
            return true;
        });
    }

    [Fact]
    public async Task A_named_area_takes_its_widgets_from_its_section_live()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PreferencesTests.PathOf(shell);
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
            File.WriteAllText(path, $"[{name}]\nwidgets = [\n  {{ widget = '{widget}', text = 'first', count = 3, options = {{ enabled = true }} }},\n" +
                                    $"  {{ widget = '{widget}', text = 'second' }},\n]\n\n" +
                                    $"[{unknown}]\nwidgets = [{{ widget = 'not-installed' }}]\n");
            try
            {
                using var area = new WidgetArea { Name = name, Defaults = [new(widget)] };
                using var other = new WidgetArea { Name = name + "other", Defaults = [new(widget)] };
                var placed = created.Where(item => item.Context.Settings.ContainsKey("text")).ToArray();
                Assert.Equal(["first", "second"], placed.Select(item => item.Context.Settings["text"]));
                Assert.Equal(3L, placed[0].Context.Settings["count"]);
                Assert.Equal(true, Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
                    placed[0].Context.Settings["options"])["enabled"]);
                var unchanged = Assert.Single(created, item => item.Context.Settings.Count == 0).View;
                await Wait.UntilAsync(() => ShellFixture.ReadLog(shell).Contains($"no component named '{unknown}'", StringComparison.Ordinal),
                    "the warning for the section no component reads");
                Assert.Contains($"[{unknown}]", File.ReadAllText(path), StringComparison.Ordinal);

                var before = created.Count;
                // Replacing the file also exercises editors that save by rename.
                var next = path + ".next";
                File.WriteAllText(next, $"[{name}]\nwidgets = [{{ widget = '{widget}', text = 'updated' }}]\n");
                File.Move(next, path, true);
                await Wait.UntilAsync(() => created.Count > before, "the saved list to rebuild");
                Assert.All(placed, item => Assert.Equal(1, item.View.Disposals));
                Assert.Equal("updated", created[^1].Context.Settings["text"]);
                Assert.Equal(0, unchanged.Disposals);
                var updated = created[^1].View;

                before = created.Count;
                var anotherUnknown = unknown + "again";
                File.WriteAllText(path, $"# A comment and an unrelated section leave the existing widgets alone.\n" +
                                        $"[{name}]\nwidgets = [{{ widget = '{widget}', text = 'updated' }}]\n" +
                                        $"[{anotherUnknown}]\n");
                await Wait.UntilAsync(() => ShellFixture.ReadLog(shell).Contains($"no component named '{anotherUnknown}'", StringComparison.Ordinal),
                    "the equivalent list to be read");
                Assert.Equal(before, created.Count);
                Assert.Equal(0, updated.Disposals);

                File.WriteAllText(path, $"[{name}]\nwidgets = []\n");
                await Wait.UntilAsync(() => updated.Disposals == 1, "the empty list to remove the widget");
                Assert.Empty(TextIn(area));
                Assert.Equal(0, unchanged.Disposals);

                before = created.Count;
                File.WriteAllText(path, $"# The area is absent, so it uses the author's defaults again.\n");
                await Wait.UntilAsync(() => created.Count > before, "the absent section to restore the defaults");
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

    [Fact]
    public async Task Renaming_an_area_reads_the_section_of_its_new_name()
    {
        await ShellFixture.On(shell =>
        {
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = "area" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            Widgets.Register(widget, context => new TextBlock
            {
                Text = context.Settings.TryGetValue("text", out var text) ? (string)text : "default"
            });
            File.WriteAllText(path, $"[{name}side]\nwidgets = [{{ widget = '{widget}', text = 'side' }}]\n");
            try
            {
                using var area = new WidgetArea { Name = name, Defaults = [new(widget)] };
                Assert.Equal(["default"], TextIn(area));

                area.Name = name + "side";
                Assert.Equal(["side"], TextIn(area));

                area.Name = "";
                Assert.Equal(["default"], TextIn(area));
            }
            finally
            {
                File.Delete(path);
            }
            return true;
        });
    }

    [Theory]
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
            var views = new List<DisposableWidget>();
            Widgets.Register(widget, _ =>
            {
                var view = new DisposableWidget();
                views.Add(view);
                return view;
            });
            File.WriteAllText(path, $"[{name}]\nwidgets = [{{ widget = '{widget}' }}]\n");
            try
            {
                using var area = new WidgetArea { Name = name };
                Assert.Single(views);
                var logLength = ShellFixture.ReadLog(shell).Length;
                File.WriteAllText(path, broken);
                await Wait.UntilAsync(() => ShellFixture.ReadLog(shell)[logLength..].Contains(diagnostic, StringComparison.Ordinal),
                    "the broken edit to be diagnosed with its location");
                Assert.Equal(broken, File.ReadAllText(path));
                Assert.Equal(0, Assert.Single(views).Disposals);
                Assert.Single(TextIn(area));

                File.WriteAllText(path, $"[{name}]\nwidgets = []\n");
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
    public async Task The_style_loads_from_its_uri_keeps_the_last_on_a_failed_load_and_leaves_the_area_its_own_resources()
    {
        await ShellFixture.On(shell =>
        {
            using var area = new WidgetArea();
            var style = new Uri("ms-appx:///WidgetTheme.xaml");
            area.Resources["author"] = "keep";
            var authors = new ResourceDictionary { ["ShellSlotSize"] = 50.0 };
            area.Resources.MergedDictionaries.Add(authors);
            area.Style = style;
            Assert.Equal(style, area.Style);
            Assert.Equal("widget theme", area.Resources["WidgetThemeMessage"]);
            var missing = new Uri("ms-appx:///Missing.xaml");
            Assert.ThrowsAny<Exception>(() => area.Style = missing);
            Assert.Equal(style, area.Style);
            Assert.Equal("widget theme", area.Resources["WidgetThemeMessage"]);
            Assert.ThrowsAny<Exception>(() => shell.Style = missing);
            Assert.Null(shell.Style);
            area.Style = null;
            Assert.False(area.Resources.ContainsKey("WidgetThemeMessage"));
            Assert.Equal("keep", area.Resources["author"]);
            Assert.Equal(50.0, area.Resources["ShellSlotSize"]);
            return true;
        });
    }

    [Fact]
    public async Task With_no_style_plain_controls_take_the_built_in_style_and_follow_the_windows_theme()
    {
        await ShellFixture.On(async _ =>
        {
            var controls = new PlainControls();
            using var area = new WidgetArea
            {
                RequestedTheme = ElementTheme.Light,
                Defaults = [new(controls.Button), new(controls.Text)]
            };
            using var window = new PanelWindow { Width = 200, Height = 60, Content = area, Visible = true };
            await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);

            var (glyphButton, glyph) = Assert.Single(controls.Buttons);
            var text = Assert.Single(controls.Lines);
            Assert.Equal((32, 32), (glyphButton.ActualWidth, glyphButton.ActualHeight));
            Assert.Equal("Segoe Fluent Icons,Segoe MDL2 Assets", glyph.FontFamily.Source);
            Assert.Equal("Segoe UI Variable Text,Segoe UI", text.FontFamily.Source);
            Assert.Equal(14, text.FontSize);
            Assert.Equal(WindowsText("Light"), ColorOf(text.Foreground));
            Assert.Equal(WindowsText("Light"), ColorOf(glyphButton.Foreground));

            area.RequestedTheme = ElementTheme.Dark;
            await Wait.UntilAsync(() => ColorOf(text.Foreground) == WindowsText("Default"),
                "the text to take Windows' dark text color");
            Assert.Equal(WindowsText("Default"), ColorOf(glyphButton.Foreground));
            return true;
        });
    }

    [Fact]
    public async Task The_shell_style_reaches_areas_and_popups_live_and_an_area_style_wins_over_it()
    {
        await ShellFixture.On(async shell =>
        {
            var other = new Uri("ms-appx:///OtherStyle.xaml");
            WidgetContext? context = null;
            var controls = new PlainControls();
            var popupWidget = Guid.NewGuid().ToString();
            Widgets.Register(popupWidget, input =>
            {
                context = input;
                return new TextBlock();
            });
            using var follows = new WidgetArea
            {
                Defaults = [new(controls.Button), new(controls.Text), new(popupWidget)]
            };
            using var own = new WidgetArea
            {
                Style = new Uri("ms-appx:///WidgetTheme.xaml"),
                Defaults = [new(controls.Text)]
            };
            var (glyphButton, glyph) = Assert.Single(controls.Buttons);
            var text = controls.Lines[0];
            var ownText = controls.Lines[1];
            using var window = new PanelWindow
            {
                Width = 400,
                Height = 60,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { follows, own } },
                Visible = true
            };
            var popupText = new TextBlock { Text = "popup" };
            using var popup = new PopupWindow
            {
                Rect = new Rect(200, 100, 200, 90),
                Content = context!.StylePopup(popupText),
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
                Assert.Equal(20, glyph.FontSize);
                Assert.Equal(("Consolas", 18.0), (popupText.FontFamily.Source, popupText.FontSize));
                Assert.Equal("widget theme", own.Resources["WidgetThemeMessage"]);
                Assert.NotEqual("Consolas", ownText.FontFamily.Source);
                Assert.Equal(ownFont, ownText.FontFamily.Source);

                shell.Style = null;
                await Wait.UntilAsync(() => glyphButton.ActualWidth == 32, "the area to return to the built-in style");
                Assert.Equal("Segoe UI Variable Text,Segoe UI", text.FontFamily.Source);
                Assert.Equal("Segoe UI Variable Text,Segoe UI", popupText.FontFamily.Source);
                Assert.Equal("widget theme", own.Resources["WidgetThemeMessage"]);
            }
            finally
            {
                shell.Style = null;
            }
            return true;
        });
    }

    [Fact]
    public async Task A_tooltip_takes_the_shell_style_live_not_its_area_style_and_a_configuration_tooltip_style_wins()
    {
        await ShellFixture.On(async shell =>
        {
            var widget = Guid.NewGuid().ToString();
            var tip = new ToolTip { Content = "tip" };
            Widgets.Register(widget, _ =>
            {
                var button = new Button { Content = "B" };
                ToolTipService.SetToolTip(button, tip);
                return button;
            });
            var other = new Uri("ms-appx:///OtherStyle.xaml");
            using var area = new WidgetArea { Style = other, Defaults = [new(widget)] };
            using var window = new PanelWindow { Width = 100, Height = 40, Content = area, Visible = true };
            await ShellFixture.LaidOut(area).WaitAsync(TestContext.Current.CancellationToken);

            async Task<(global::Windows.UI.Color Background, global::Windows.UI.Color Foreground, string Font)> Open()
            {
                tip.IsOpen = true;
                await Wait.UntilAsync(() => tip.ActualWidth > 0, "the tooltip to open");
                var look = (ColorOf(tip.Background), ColorOf(tip.Foreground), tip.FontFamily.Source);
                tip.IsOpen = false;
                await Wait.UntilAsync(() => tip.ActualWidth == 0, "the tooltip to close");
                return look;
            }

            try
            {
                var theme = shell.IsDark ? "Default" : "Light";
                Assert.Equal((WindowsColor(theme, "SolidBackgroundFillColorBase"),
                    WindowsColor(theme, "TextFillColorPrimary"), "Segoe UI Variable Text,Segoe UI"), await Open());

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

    private static global::Windows.UI.Color ColorOf(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    /// <summary>The text color Windows gives a theme: "Light", or "Default" for dark.</summary>
    private static global::Windows.UI.Color WindowsText(string theme) => WindowsColor(theme, "TextFillColorPrimary");

    /// <summary>A color Windows gives a theme: "Light", or "Default" for dark.</summary>
    private static global::Windows.UI.Color WindowsColor(string theme, string key)
    {
        var controls = Application.Current.Resources.MergedDictionaries.OfType<XamlControlsResources>().Single();
        return (global::Windows.UI.Color)((ResourceDictionary)controls.ThemeDictionaries[theme])[key];
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
            Widgets.Register(Button, _ =>
            {
                var glyph = new FontIcon { Glyph = "" };
                var button = new Button { Content = glyph };
                Buttons.Add((button, glyph));
                return button;
            });
            Widgets.Register(Text, _ =>
            {
                var line = new TextBlock { Text = "12:00" };
                Lines.Add(line);
                return line;
            });
        }

        public string Button { get; } = Guid.NewGuid().ToString();
        public string Text { get; } = Guid.NewGuid().ToString();
        public List<(Button Button, FontIcon Glyph)> Buttons { get; } = [];
        public List<TextBlock> Lines { get; } = [];
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
