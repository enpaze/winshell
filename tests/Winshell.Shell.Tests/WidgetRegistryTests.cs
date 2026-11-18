using System.Reflection;

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
    public void A_null_WidgetName_fails_naming_its_type()
    {
        var error = Assert.Throws<InvalidOperationException>(() => WidgetRegistry.Of([typeof(Unnamed)]));

        Assert.Contains(typeof(Unnamed).FullName!, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_author_error_is_named_in_one_error()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            WidgetRegistry.Of([typeof(Clock), typeof(ClockWidget), typeof(NeedsText), typeof(Blank)]));

        Assert.Contains("Two widgets are named clock", error.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(NeedsText).FullName!, error.Message, StringComparison.Ordinal);
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
        var names = WidgetRegistry.Discover(new Log()).Names.ToList();

        Assert.Contains("message", names);
        Assert.Contains("clock", names);
        Assert.Contains("hello", names);
        Assert.DoesNotContain("greeting", names);
        Assert.DoesNotContain("network-speed", names);
        Assert.DoesNotContain("placement", names);
    }

    [Fact]
    public void An_assembly_that_cannot_be_read_loaded_bound_or_enumerated_is_skipped_with_one_warning()
    {
        using var folder = new TempFolder();
        var shell = typeof(Widget).Assembly.GetName();
        var locked = Path.Combine(folder.Path, "Locked.dll");
        File.Copy(typeof(WidgetRegistryTests).Assembly.Location, locked);
        var text = Path.Combine(folder.Path, "Text.dll");
        File.WriteAllText(text, "not an assembly");
        var native = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
        var unloadable = "Unloadable" + Guid.NewGuid().ToString("N");
        var missingBase = GeneratedAssembly.Write(folder.Path, unloadable, [shell],
            ("Thing", new AssemblyName("Missing" + Guid.NewGuid().ToString("N")) { Version = new Version(1, 0, 0, 0) },
                "Missing.Base"));
        var newer = new AssemblyName(shell.Name!) { Version = new Version(99, 0, 0, 0) };
        var otherVersion = "OtherVersion" + Guid.NewGuid().ToString("N");
        var builtAgainstNewer = GeneratedAssembly.Write(folder.Path, otherVersion, [], ("Clock", newer, "Winshell.Widget"));
        List<string> warnings = [];
        var log = new Log { Sink = (level, category, message) => warnings.Add($"{level} {category}: {message}") };
        string[] skipped = [locked, text, native, missingBase, builtAgainstNewer];

        WidgetRegistry names;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            names = WidgetRegistry.Discover([.. skipped, typeof(WidgetRegistryTests).Assembly.Location],
                new FolderLoadContext(folder.Path, unloadable, otherVersion), log);

        Assert.Contains("message", names.Names);
        Assert.Equal(skipped.Length, warnings.Count);
        foreach (var path in skipped)
        {
            var warning = Assert.Single(warnings, warning => warning.Contains(path, StringComparison.Ordinal));
            Assert.StartsWith($"Warn widgets: Skipped {path} while looking for widgets: ", warning, StringComparison.Ordinal);
            Assert.True(warning.Length > $"Warn widgets: Skipped {path} while looking for widgets: ".Length,
                $"The warning gives no reason: {warning}");
        }
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

    [WidgetName(null!)]
    private sealed class Unnamed : Widget
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
