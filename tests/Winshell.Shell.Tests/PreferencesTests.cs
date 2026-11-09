using System.ComponentModel;

namespace Winshell.ShellTests;

/// <summary>
/// The shell's preferences file, <c>preferences\&lt;shell id&gt;.toml</c>: the configuration's own keys before the first
/// section, and a section for each component.
/// </summary>
public sealed class PreferencesTests
{
    private sealed partial class Own : Preferences
    {
        [TomlComment("Show 13:00 rather than 1:00 PM.")]
        public bool Clock24
        {
            get;
            set => Set(ref field, value);
        }
    }

    private sealed partial class BarPreferences(string name) : Preferences(name)
    {
        [TomlComment("top, bottom, left or right.")]
        public string Edge
        {
            get;
            set => Set(ref field, value);
        } = "top";

        public int Size
        {
            get;
            set => Set(ref field, value);
        } = 32;
    }

    internal static string PathOf(Shell shell)
    {
        return Path.Combine(shell.ConfigRoot, "preferences", shell.ShellId + ".toml");
    }

    private static List<string> Changes(INotifyPropertyChanged preferences)
    {
        var changes = new List<string>();
        preferences.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        return changes;
    }

    [Fact]
    public async Task Sections_share_one_file_and_a_save_changes_only_the_section_that_changed()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bar = "bar" + Guid.NewGuid().ToString("N");
            var dock = "dock" + Guid.NewGuid().ToString("N");
            File.WriteAllText(path, $"clock24 = true\n\n[{bar}]\nedge = \"left\"\n\n[{dock}]\nedge = \"bottom\"\nsize = 48\n");
            try
            {
                using var own = new Own();
                using var top = new BarPreferences(bar);
                using var bottom = new BarPreferences(dock);
                Assert.True(own.Clock24);
                Assert.Equal(("left", 32), (top.Edge, top.Size));
                Assert.Equal(("bottom", 48), (bottom.Edge, bottom.Size));
                var ownChanges = Changes(own);
                var topChanges = Changes(top);
                var bottomChanges = Changes(bottom);

                File.WriteAllText(path, $"clock24 = true\n\n[{bar}]\nedge = \"left\"\n\n[{dock}]\nedge = \"bottom\"\nsize = 64\n");
                await Wait.UntilAsync(() => bottom.Size == 64, "the saved size to reach its section");
                Assert.Equal([nameof(BarPreferences.Size)], bottomChanges);
                Assert.Empty(topChanges);
                Assert.Empty(ownChanges);

                File.WriteAllText(path, $"clock24 = true\n\n[{bar}]\n\n[{dock}]\nedge = \"bottom\"\nsize = 64\n");
                await Wait.UntilAsync(() => top.Edge == "top", "the removed edge to go back to its default");
                Assert.Equal([nameof(BarPreferences.Edge)], topChanges);
                Assert.Equal([nameof(BarPreferences.Size)], bottomChanges);
                Assert.Empty(ownChanges);
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        });
    }

    [Fact]
    public async Task Assigning_a_property_writes_it_into_its_section_and_leaves_the_rest()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bar = "bar" + Guid.NewGuid().ToString("N");
            File.WriteAllText(path, $"# mine\nclock24 = false\n\n[{bar}]\n# the edge\nedge = \"left\"\nother = 1\n");
            try
            {
                using var own = new Own();
                using var top = new BarPreferences(bar);

                top.Size = 40;
                own.Clock24 = true;

                await Wait.UntilAsync(() => File.ReadAllText(path).Contains("size", StringComparison.Ordinal),
                    "the assigned values to be written");
                Assert.Equal($"# mine\nclock24 = true\n\n[{bar}]\n# the edge\nedge = \"left\"\nother = 1\nsize = 40\n",
                    File.ReadAllText(path));
                Assert.Equal("left", top.Edge);
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        });
    }

    [Fact]
    public async Task A_missing_file_is_written_with_each_sections_defaults_commented_out()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PathOf(shell);
            File.Delete(path);
            var bar = "bar" + Guid.NewGuid().ToString("N");
            var name = "area" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            Widgets.Register(widget, _ => new Microsoft.UI.Xaml.Controls.TextBlock { Text = "default" });
            try
            {
                using var own = new Own();
                using var top = new BarPreferences(bar);
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
                await Wait.UntilAsync(() => File.Exists(path), "the missing file to be written");

                var text = File.ReadAllText(path);
                Assert.All(text.Split('\n').Where(line => line.Length > 0),
                    line => Assert.True(line.StartsWith('#') || line.StartsWith('['), line));
                Assert.Contains("# Show 13:00 rather than 1:00 PM.\n# clock24 = false\n", text, StringComparison.Ordinal);
                Assert.Contains("# top, bottom, left or right.\n# edge = \"top\"\n", text, StringComparison.Ordinal);
                // The last line of each block after the file's own comment is a key, commented out.
                var uncommented = string.Join("\n\n", text.Split("\n\n").Skip(1).Select(block =>
                {
                    var lines = block.TrimEnd('\n').Split('\n');
                    if (lines[^1].StartsWith("# ", StringComparison.Ordinal))
                        lines[^1] = lines[^1][2..];
                    return string.Join('\n', lines);
                }));
                Assert.True(TomlDocument.TryParse(path, uncommented, out var document, out var error), error);
                Assert.Equal(false, document.Values["clock24"]);
                var section = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(document.Values[bar]);
                Assert.Equal("top", section["edge"]);
                Assert.Equal(32L, section["size"]);
                section = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(document.Values[name]);
                var rows = Assert.IsAssignableFrom<IReadOnlyList<object?>>(section["widgets"]);
                Assert.Equal(2, rows.Count);
                var first = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(rows[0]);
                Assert.Equal(widget, first["widget"]);
                Assert.Equal("a \"quoted\" value\nand a line break", first["text"]);
                Assert.Equal(7L, first["count"]);
                Assert.Equal(true, Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(first["nested"])["enabled"]);
                Assert.Equal(widget, Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(rows[1])["widget"]);
                section = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(document.Values[name + "empty"]);
                Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<object?>>(section["widgets"]));
                Assert.Equal(("top", 32), (top.Edge, top.Size));
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        });
    }
}
