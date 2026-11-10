using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

/// <summary>A panel shows a Reactor component, and the component renders again when a service object it observes changes.</summary>
[Trait("Category", "Window")]
public sealed class ComponentWindowTests
{
    [Fact]
    public async Task A_panel_shows_the_controls_a_component_rendered()
    {
        var text = await ShellFixture.On(async _ =>
        {
            var host = new ReactorHostControl(new Greeting());
            using var panel = Panel(host);
            await ShellFixture.LaidOut(host);
            await host.WaitForIdleAsync();
            return ShownText(host);
        });

        Assert.Equal("Hello", text);
    }

    [Fact]
    public async Task A_component_renders_again_when_the_service_object_it_observes_changes()
    {
        var text = await ShellFixture.On(async _ =>
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

        Assert.Equal("full", text);
    }

    [Fact]
    public async Task A_component_renders_again_when_an_item_in_a_service_list_changes()
    {
        var text = await ShellFixture.On(async _ =>
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

        Assert.Equal("after", text);
    }

    private static PanelWindow Panel(FrameworkElement content)
    {
        return new PanelWindow
        {
            Anchors = Edges.Top | Edges.Left,
            Width = 220,
            Height = 90,
            Content = content,
            Visible = true
        };
    }

    private static string ShownText(DependencyObject root)
    {
        var blocks = new List<TextBlock>();
        Collect(root, blocks);
        return Assert.Single(blocks).Text;

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

    /// <summary>A service object: the base every service exposes, with one property a component can observe.</summary>
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

    /// <summary>
    /// A service object whose only observable property is the list. The getter does not report a read, so the
    /// component renders again only when the list itself raises for an item.
    /// </summary>
    private sealed class Listed : ObservableObject
    {
        public Listed()
        {
            Items.Add(new Entry());
        }

        public ObservableList<Entry> Items { get; } = new();
    }

    /// <summary>One item of <see cref="Listed"/>.</summary>
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
