namespace Winshell.ShellTests;

public sealed class WidgetRegistryTests
{
    [Theory]
    [InlineData("Clock", "clock")]
    [InlineData("ClockWidget", "clock")]
    [InlineData("NetworkSpeed", "network-speed")]
    [InlineData("NetworkSpeedWidget", "network-speed")]
    [InlineData("CPUUsage", "cpu-usage")]
    [InlineData("IPAddressWidget", "ip-address")]
    [InlineData("Clock24", "clock-24")]
    [InlineData("Win11Bar", "win-11-bar")]
    [InlineData("CPU", "cpu")]
    [InlineData("Widget", "widget")]
    [InlineData("WidgetWidget", "widget")]
    [InlineData("WidgetArea", "widget-area")]
    public void A_widget_is_named_after_its_class_in_kebab_case_without_a_trailing_Widget(string type, string name)
    {
        Assert.Equal(name, WidgetRegistry.NameOf(type));
    }

    [Fact]
    public void WidgetName_overrides_the_class_name()
    {
        Assert.Equal(["hello", "network-speed"], Names(typeof(Greeting), typeof(NetworkSpeed)));
    }

    [Fact]
    public void Abstract_and_generic_classes_are_not_widgets()
    {
        Assert.Equal(["network-speed"], Names(typeof(AbstractWidget), typeof(GenericWidget<>), typeof(NetworkSpeed)));
    }

    [Fact]
    public void Two_widgets_with_one_name_fail_naming_both_types()
    {
        var error = Assert.Throws<InvalidOperationException>(() => WidgetRegistry.Of([typeof(Clock), typeof(ClockWidget)]));

        Assert.Contains("Two widgets are named clock", error.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(Clock).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(ClockWidget).FullName!, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_widget_with_no_public_parameterless_constructor_fails_naming_its_type()
    {
        var error = Assert.Throws<InvalidOperationException>(() => WidgetRegistry.Of([typeof(NeedsText)]));

        Assert.Contains(typeof(NeedsText).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Contains("no public parameterless constructor", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blank_WidgetName_fails_naming_its_type()
    {
        var error = Assert.Throws<InvalidOperationException>(() => WidgetRegistry.Of([typeof(Blank)]));

        Assert.Contains(typeof(Blank).FullName!, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_name_added_twice_fails()
    {
        var registry = WidgetRegistry.Of([]);
        registry.Add<Clock>("clock");

        Assert.Throws<InvalidOperationException>(() => registry.Add<NetworkSpeed>("clock"));
    }

    [Fact]
    public void Discovery_finds_the_public_widgets_of_the_program_and_skips_the_rest()
    {
        var names = WidgetRegistry.Discover().Names.ToList();

        Assert.Contains("message", names);
        Assert.Contains("clock", names);
        Assert.Contains("hello", names);
        Assert.DoesNotContain("greeting", names);
        Assert.DoesNotContain("network-speed", names);
        Assert.DoesNotContain("placement", names);
    }

    private static string[] Names(params Type[] types) =>
        [.. WidgetRegistry.Of(types).Names.Order(StringComparer.Ordinal)];

    [WidgetName("hello")]
    private sealed class Greeting : Widget
    {
        public override Element Render() => TextBlock("Hello");
    }

    private sealed class NetworkSpeed : Widget
    {
        public override Element Render() => TextBlock("");
    }

    private sealed class Clock : Widget
    {
        public override Element Render() => TextBlock("");
    }

    private sealed class ClockWidget : Widget
    {
        public override Element Render() => TextBlock("");
    }

    [WidgetName(" ")]
    private sealed class Blank : Widget
    {
        public override Element Render() => TextBlock("");
    }

    private abstract class AbstractWidget : Widget;

    private sealed class GenericWidget<T> : Widget
    {
        public override Element Render() => TextBlock(typeof(T).Name);
    }

    private sealed class NeedsText(string text) : Widget
    {
        public override Element Render() => TextBlock(text);
    }
}
