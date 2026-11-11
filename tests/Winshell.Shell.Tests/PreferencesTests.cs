using System.ComponentModel;
using System.Text;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

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

        public string? Note
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

    /// <summary>Completes on the turn after the ones already posted, such as the write an assignment posted.</summary>
    private static Task NextTurn(Shell shell)
    {
        var turn = new TaskCompletionSource();
        shell.Loop.Post(turn.SetResult);
        return turn.Task;
    }

    /// <summary>
    /// Runs <paramref name="test"/> on the shell's thread with a new section name and the preferences file holding
    /// <paramref name="text"/> of it, and deletes the file after.
    /// </summary>
    private static Task WithFile(Func<string, string> text, Func<Shell, string, string, Task> test)
    {
        return ShellFixture.On(async shell =>
        {
            var path = PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bar = "bar" + Guid.NewGuid().ToString("N");
            File.WriteAllText(path, text(bar));
            try
            {
                await test(shell, path, bar);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }

            return true;
        });
    }

    /// <summary>The paths and errors <paramref name="shell"/>'s log hears about a file it stops or starts reading.</summary>
    private static List<(string Path, string Error)> FileErrors(Shell shell, out Action stop)
    {
        var heard = new List<(string Path, string Error)>();
        Action<string, string> hear = (path, error) => heard.Add((path, error));
        shell.Log.FileErrorChanged += hear;
        stop = () => shell.Log.FileErrorChanged -= hear;
        return heard;
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
    public async Task An_assigned_property_is_written_on_a_later_turn_into_its_section_and_the_file_is_still_followed()
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

                // Written on a later turn of the loop.
                Assert.Equal($"# mine\nclock24 = false\n\n[{bar}]\n# the edge\nedge = \"left\"\nother = 1\n",
                    File.ReadAllText(path));
                await NextTurn(shell);
                Assert.Equal($"# mine\nclock24 = true\n\n[{bar}]\n# the edge\nedge = \"left\"\nother = 1\nsize = 40\n",
                    File.ReadAllText(path));
                Assert.Equal("left", top.Edge);

                File.WriteAllText(path, $"clock24 = true\n\n[{bar}]\nedge = \"bottom\"\nsize = 40\n");
                await Wait.UntilAsync(() => top.Edge == "bottom", "a hand save after the write to be read");
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        });
    }

    [Fact]
    public async Task Assigning_a_property_of_a_section_the_file_lacks_writes_it_under_its_own_header()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bar = "bar" + Guid.NewGuid().ToString("N");
            File.WriteAllText(path, "clock24 = true\n");
            try
            {
                using var top = new BarPreferences(bar);

                top.Size = 40;

                await Wait.UntilAsync(() => File.ReadAllText(path).Contains("size = 40", StringComparison.Ordinal),
                    "the assigned size to be written");
                Assert.Equal(
                    $"clock24 = true\n\n[{bar}]\n\n# top, bottom, left or right.\n# edge = \"top\"\n\n# size = 32\nsize = 40\n",
                    File.ReadAllText(path));
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        });
    }

    [Fact]
    public async Task A_component_made_after_the_file_exists_adds_its_section_once_with_defaults_commented_out()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bar = "bar" + Guid.NewGuid().ToString("N");
            File.WriteAllText(path, "# mine\nclock24 = true\n");
            try
            {
                using var top = new BarPreferences(bar);

                await Wait.UntilAsync(() => File.ReadAllText(path).Contains($"[{bar}]", StringComparison.Ordinal),
                    "the new section to be added");
                Assert.Equal(
                    $"# mine\nclock24 = true\n\n[{bar}]\n\n# top, bottom, left or right.\n# edge = \"top\"\n\n# size = 32\n",
                    File.ReadAllText(path));

                // Taken out by hand, the section stays out: the check that names the unknown section adds nothing.
                var logLength = ShellFixture.ReadLog(shell).Length;
                var unknown = "unknown" + Guid.NewGuid().ToString("N");
                var edited = $"# mine\nclock24 = false\n\n[{unknown}]\n";
                File.WriteAllText(path, edited);
                await Wait.UntilAsync(() => ShellFixture.ReadLog(shell)[logLength..]
                        .Contains($"no component named '{unknown}'", StringComparison.Ordinal),
                    "the hand save to be checked");
                Assert.Equal(edited, File.ReadAllText(path));
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
            // An earlier test may have written it. On a fresh machine there is no folder yet either.
            if (File.Exists(path))
                File.Delete(path);
            var bar = "bar" + Guid.NewGuid().ToString("N");
            var name = "area" + Guid.NewGuid().ToString("N");
            var widget = Guid.NewGuid().ToString();
            Widgets.Register<DefaultWidget>(widget);
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
                // A key with no default has nothing to show.
                Assert.DoesNotContain("note", text, StringComparison.Ordinal);
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

    [Fact]
    public async Task A_value_left_unchanged_is_named_once_and_stays_in_the_file_until_it_is_assigned()
    {
        await WithFile(bar => $"[{bar}]\nedge = 3\nsize = 4\n", async (shell, path, bar) =>
        {
            var logLength = ShellFixture.ReadLog(shell).Length;
            using var top = new BarPreferences(bar);
            Assert.Equal(("top", 4), (top.Edge, top.Size));

            top.Size = 5;
            await NextTurn(shell);
            Assert.Equal($"[{bar}]\nedge = 3\nsize = 5\n", File.ReadAllText(path));

            // The watch hears the write on another thread. Let it run, then read on the loop.
            Wait.ForNothingToHappen(TimeSpan.FromMilliseconds(200));
            await NextTurn(shell);
            var warning = $"{path}[2:1]: edge should be a string, not 3, so it was left unchanged";
            Assert.Single(ShellFixture.ReadLog(shell)[logLength..].Split(warning)[1..]);

            top.Edge = "left";
            await NextTurn(shell);
            Assert.Equal($"[{bar}]\nedge = \"left\"\nsize = 5\n", File.ReadAllText(path));
        });
    }

    [Fact]
    public async Task A_write_starts_from_the_file_on_disk_even_after_a_torn_read()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"le", async (shell, path, bar) =>
        {
            using var top = new BarPreferences(bar);
            Assert.Equal("top", top.Edge);

            top.Size = 40;
            // The rest of the save lands after the assignment and before its write.
            File.WriteAllText(path, $"[{bar}]\nedge = \"left\" # kept\nsize = 9\n\n[other]\nkept = true\n");
            await NextTurn(shell);

            Assert.Equal($"[{bar}]\nedge = \"left\" # kept\nsize = 40\n\n[other]\nkept = true\n", File.ReadAllText(path));
            Assert.Equal(("left", 40), (top.Edge, top.Size));
        });
    }

    [Fact]
    public async Task A_file_another_program_holds_is_neither_taken_for_missing_nor_written()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", async (shell, path, bar) =>
        {
            var text = File.ReadAllText(path);
            using var first = new BarPreferences(bar);
            var logLength = ShellFixture.ReadLog(shell).Length;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // Made while the file is held, it reads the file and cannot.
                using var second = new BarPreferences(bar);
                Assert.Equal(("left", "left"), (first.Edge, second.Edge));

                first.Size = 40;
                await NextTurn(shell);
                var log = ShellFixture.ReadLog(shell)[logLength..];
                Assert.Contains("the file was left unchanged", log, StringComparison.Ordinal);
                Assert.DoesNotContain("failed to write", log, StringComparison.Ordinal);
            }

            Assert.Equal(text, File.ReadAllText(path));
        });
    }

    [Fact]
    public async Task A_byte_order_mark_is_kept_and_a_write_leaves_no_temporary_file()
    {
        await WithFile(_ => "", async (shell, path, bar) =>
        {
            File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes($"[{bar}]\nedge = \"left\"\n")]);
            using var top = new BarPreferences(bar);
            Assert.Equal("left", top.Edge);

            top.Size = 40;
            await NextTurn(shell);

            var written = File.ReadAllBytes(path);
            Assert.Equal([0xEF, 0xBB, 0xBF], written[..3]);
            Assert.Equal($"[{bar}]\nedge = \"left\"\nsize = 40\n", Encoding.UTF8.GetString(written[3..]));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".tmp-*"));
        });
    }

    [Fact]
    public async Task A_write_that_fails_is_named_and_the_value_waits_for_the_next_one()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", async (shell, path, bar) =>
        {
            var text = File.ReadAllText(path);
            using var top = new BarPreferences(bar);
            var logLength = ShellFixture.ReadLog(shell).Length;
            File.SetAttributes(path, FileAttributes.ReadOnly);

            top.Size = 40;
            await NextTurn(shell);
            Assert.Contains($"failed to write {path}", ShellFixture.ReadLog(shell)[logLength..], StringComparison.Ordinal);
            Assert.Equal(text, File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".tmp-*"));

            File.SetAttributes(path, FileAttributes.Normal);
            top.Edge = "bottom";
            await NextTurn(shell);
            Assert.Equal($"[{bar}]\nedge = \"bottom\"\nsize = 40\n", File.ReadAllText(path));
        });
    }

    [Fact]
    public async Task Disposing_a_section_finishes_its_write_on_its_way()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", (_, path, bar) =>
        {
            var top = new BarPreferences(bar);

            top.Size = 40;
            top.Dispose();

            Assert.Equal($"[{bar}]\nedge = \"left\"\nsize = 40\n", File.ReadAllText(path));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task A_disposed_section_does_not_follow_or_write()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", async (shell, path, bar) =>
        {
            using var live = new BarPreferences(bar);
            var gone = new BarPreferences(bar);
            gone.Dispose();

            File.WriteAllText(path, $"[{bar}]\nedge = \"bottom\"\n");
            await Wait.UntilAsync(() => live.Edge == "bottom", "the hand save to be read");
            Assert.Equal("left", gone.Edge);

            gone.Size = 40;
            live.Edge = "right";
            await NextTurn(shell);
            Assert.Equal($"[{bar}]\nedge = \"right\"\n", File.ReadAllText(path));
        });
    }

    [Fact]
    public async Task A_broken_file_blocks_a_write_and_its_fix_replaces_what_could_not_be_written()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", async (shell, path, bar) =>
        {
            using var top = new BarPreferences(bar);
            var heard = FileErrors(shell, out var stop);
            try
            {
                var broken = $"[{bar}]\nedge = \"broken";
                File.WriteAllText(path, broken);
                await Wait.UntilAsync(() => heard.Count > 0, "the broken file to be named");
                Assert.Equal(path, heard[0].Path);
                Assert.StartsWith(path + "[2:", heard[0].Error, StringComparison.Ordinal);
                Assert.Equal("left", top.Edge);

                var logLength = ShellFixture.ReadLog(shell).Length;
                top.Size = 40;
                await NextTurn(shell);
                Assert.Equal(broken, File.ReadAllText(path));
                Assert.Contains("the file was left unchanged", ShellFixture.ReadLog(shell)[logLength..],
                    StringComparison.Ordinal);

                File.WriteAllText(path, $"[{bar}]\nedge = \"bottom\"\n");
                await Wait.UntilAsync(() => top.Edge == "bottom", "the fixed file to be read");
                Assert.Equal([(path, heard[0].Error), (path, "")], heard);
                Assert.Equal(32, top.Size);
                Assert.Equal($"[{bar}]\nedge = \"bottom\"\n", File.ReadAllText(path));
            }
            finally
            {
                stop();
            }
        });
    }

    [Fact]
    public async Task A_file_past_the_limit_keeps_the_values_and_the_file_until_it_is_back_under()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", async (shell, path, bar) =>
        {
            var text = File.ReadAllText(path);
            using var top = new BarPreferences(bar);
            var heard = FileErrors(shell, out var stop);
            try
            {
                var oversized = Encoding.UTF8.GetBytes(text + "# " + new string('x', 1024 * 1024) + "\n");
                File.WriteAllBytes(path, oversized);
                await Wait.UntilAsync(() => heard.Count > 0, "the file past the limit to be named");
                Assert.Contains("above the limit", heard[0].Error, StringComparison.Ordinal);
                Assert.Equal("left", top.Edge);

                top.Size = 40;
                await NextTurn(shell);
                Assert.Equal(oversized, File.ReadAllBytes(path));

                // The text it read before the file grew.
                File.WriteAllText(path, text);
                await Wait.UntilAsync(() => heard.Count > 1, "the file back under the limit to be read");
                Assert.Equal((path, ""), heard[1]);
            }
            finally
            {
                stop();
            }
        });
    }

    [Fact]
    public async Task A_deleted_file_brings_back_the_defaults_and_is_written_again()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", async (_, path, bar) =>
        {
            using var top = new BarPreferences(bar);
            Assert.Equal("left", top.Edge);

            File.Delete(path);

            await Wait.UntilAsync(() => File.Exists(path), "the deleted file to be written again");
            Assert.Equal("top", top.Edge);
            Assert.Contains($"[{bar}]\n\n# top, bottom, left or right.\n# edge = \"top\"\n", File.ReadAllText(path),
                StringComparison.Ordinal);
        });
    }

    /// <summary>A placement the missing-file test names, so the area can write that name into the defaults.</summary>
    private sealed class DefaultWidget : Component<WidgetContext>
    {
        public override Element Render() => TextBlock("default");
    }
}
