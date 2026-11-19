using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

[TestClass]
[TestCategory("Window")]
public sealed class ComponentWindowTests
{
    [TestMethod]
    public async Task A_panel_shows_the_controls_a_component_rendered()
    {
        var text = await ShellFixture.On(async shell =>
        {
            var host = new ReactorHostControl(new Greeting());
            using var panel = Panel(host);
            await ShellFixture.LaidOut(host);
            await host.WaitForIdleAsync();
            return ShownText(host);
        });

        Assert.AreEqual("Hello", text);
    }

    [TestMethod]
    public async Task A_component_renders_again_when_the_service_object_it_observes_changes()
    {
        var text = await ShellFixture.On(async shell =>
        {
            var level = new Level();
            var host = new ReactorHostControl(new Gauge(level));
            using var panel = Panel(host);
            await ShellFixture.LaidOut(host);
            await host.WaitForIdleAsync();

            level.Text = "full";
            await host.WaitForIdleAsync();
            return ShownText(host);
        });

        Assert.AreEqual("full", text);
    }

    [TestMethod]
    public async Task A_component_renders_again_when_an_item_in_a_service_list_changes()
    {
        var text = await ShellFixture.On(async shell =>
        {
            var service = new Listed();
            var host = new ReactorHostControl(new ListedLabel(service));
            using var panel = Panel(host);
            await ShellFixture.LaidOut(host);
            await host.WaitForIdleAsync();

            service.Items[0].Label = "after";
            await host.WaitForIdleAsync();
            return ShownText(host);
        });

        Assert.AreEqual("after", text);
    }

    [TestMethod]
    [DataRow("panel")]
    [DataRow("popup")]
    [DataRow("floating")]
    public async Task A_component_window_shows_its_component_on_a_surface(string kind)
    {
        var (text, styled, background) = await ShellFixture.On(async shell =>
        {
            var other = TestStyles.Other();
            shell.Style = other;
            try
            {
                using var window = Shown<Greeting>(kind);
                var host = (ReactorHostControl)window.Content!;
                await ShellFixture.LaidOut(host);
                return (ShownText(host), other.Background, BarTests.BackgroundOf(host));
            }
            finally
            {
                shell.Style = new();
            }
        });

        Assert.AreEqual("Hello", text);
        Assert.AreEqual(styled, background);
    }

    [TestMethod]
    [DataRow("panel")]
    [DataRow("popup")]
    [DataRow("floating")]
    public async Task Disposing_a_component_window_unmounts_its_component(string kind)
    {
        var (shown, hidden, disposed) = await ShellFixture.On(async shell =>
        {
            Tracked.Mounted = 0;
            var window = Shown<Tracked>(kind);
            await ShellFixture.LaidOut((FrameworkElement)window.Content!);
            await Wait.UntilAsync(() => Tracked.Mounted == 1, "the component to mount");
            var whileShown = Tracked.Mounted;
            window.Visible = false;
            var whileHidden = Tracked.Mounted;
            window.Dispose();
            return (whileShown, whileHidden, Tracked.Mounted);
        });

        Assert.AreEqual(1, shown);
        Assert.AreEqual(1, hidden);
        Assert.AreEqual(0, disposed);
    }

    [TestMethod]
    [DataRow("panel")]
    [DataRow("popup")]
    [DataRow("floating")]
    public async Task Two_windows_of_one_component_show_the_props_each_was_given(string kind)
    {
        var texts = await ShellFixture.On(async shell =>
        {
            using var first = Shown(kind, "first");
            using var second = Shown(kind, "second");
            var firstHost = (ReactorHostControl)first.Content!;
            var secondHost = (ReactorHostControl)second.Content!;
            await ShellFixture.LaidOut(firstHost);
            await ShellFixture.LaidOut(secondHost);
            return (ShownText(firstHost), ShownText(secondHost));
        });

        Assert.AreEqual(("first", "second"), texts);
    }

    [TestMethod]
    public async Task Toggle_shows_a_hidden_window_and_hides_a_shown_one()
    {
        var states = await ShellFixture.On(shell =>
        {
            using var panel = shell.Add(new PanelWindow<Greeting>
            {
                Edge = Edge.Top,
                Align = PanelAlign.Start,
                Width = 220,
                Height = 90,
                Visible = false
            });
            panel.Toggle();
            var opened = (panel.Visible, panel.Host!.Place.IsVisible);
            panel.Toggle();
            var closed = (panel.Visible, panel.Host!.Place.IsVisible);
            return (opened, closed);
        });

        Assert.AreEqual((true, true), states.opened);
        Assert.AreEqual((false, false), states.closed);
    }

    private static ShellWindow Shown<T>(string kind) where T : Component, new()
    {
        return kind switch
        {
            "panel" => Shell.Current.Add(new PanelWindow<T> { Edge = Edge.Top, Align = PanelAlign.Start, Width = 220, Height = 90 }),
            "popup" => Shell.Current.Add(new PopupWindow<T> { Width = 220, Height = 90, Visible = true }),
            _ => Shell.Current.Add(new FloatingWindow<T> { Title = "Winshell test", Width = 320, Height = 200 })
        };
    }

    private static ShellWindow Shown(string kind, string text)
    {
        return kind switch
        {
            "panel" => Shell.Current.Add(new PanelWindow<Label, string>(text) { Edge = Edge.Top, Align = PanelAlign.Start, Width = 220, Height = 90 }),
            "popup" => Shell.Current.Add(new PopupWindow<Label, string>(text) { Width = 220, Height = 90, Visible = true }),
            _ => Shell.Current.Add(new FloatingWindow<Label, string>(text) { Title = "Winshell test", Width = 320, Height = 200 })
        };
    }

    private static PanelWindow Panel(FrameworkElement content)
    {
        return Shell.Current.Add(new PanelWindow
        {
            Edge = Edge.Top,
            Align = PanelAlign.Start,
            Width = 220,
            Height = 90,
            Content = content
        });
    }

    private static string ShownText(DependencyObject root)
    {
        var blocks = new List<TextBlock>();
        Collect(root, blocks);
        return Assert.ContainsSingle(blocks).Text;

        static void Collect(DependencyObject node, List<TextBlock> blocks)
        {
            if (node is TextBlock block)
                blocks.Add(block);
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
                Collect(VisualTreeHelper.GetChild(node, i), blocks);
        }
    }

    private sealed class Greeting : Component
    {
        public override Element Render()
        {
            return TextBlock("Hello");
        }
    }

    private sealed class Label : Component<string>
    {
        public override Element Render()
        {
            return TextBlock(Props);
        }
    }

    private sealed class Tracked : Component
    {
        public static int Mounted;

        public override Element Render()
        {
            UseEffect(() =>
            {
                Mounted++;
                return () => Mounted--;
            }, false);
            return TextBlock("tracked");
        }
    }

    private sealed class Level : ObservableObject
    {
        public string Text
        {
            get => field;
            set => Set(ref field, value);
        } = "low";
    }

    private sealed class Gauge : Component
    {
        private readonly Level _level;

        public Gauge(Level level)
        {
            _level = level;
        }

        public override Element Render()
        {
            var level = UseObservable(_level);
            return TextBlock(level.Text);
        }
    }

    private sealed class Listed : ObservableObject
    {
        public Listed()
        {
            Items.Add(new Entry());
        }

        public ObservableList<Entry> Items { get; } = new();
    }

    private sealed class Entry : ObservableObject
    {
        public string Label
        {
            get => field;
            set => Set(ref field, value);
        } = "before";
    }

    private sealed class ListedLabel : Component
    {
        private readonly Listed _service;

        public ListedLabel(Listed service)
        {
            _service = service;
        }

        public override Element Render()
        {
            var service = UseObservableTree(_service);
            return TextBlock(service.Items[0].Label);
        }
    }
}
