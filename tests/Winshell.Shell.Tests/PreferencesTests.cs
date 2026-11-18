using System.ComponentModel;
using System.Text;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

[TestClass]
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

    private static Task NextTurn(Shell shell)
    {
        var turn = new TaskCompletionSource();
        shell.Loop.Post(turn.SetResult);
        return turn.Task;
    }

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

    private static List<(string Path, string Error)> FileErrors(Shell shell, out Action stop)
    {
        var heard = new List<(string Path, string Error)>();
        Action<string, string> hear = (path, error) => heard.Add((path, error));
        shell.Log.FileErrorChanged += hear;
        stop = () => shell.Log.FileErrorChanged -= hear;
        return heard;
    }

    [TestMethod]
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
                Assert.IsTrue(own.Clock24);
                Assert.AreEqual(("left", 32), (top.Edge, top.Size));
                Assert.AreEqual(("bottom", 48), (bottom.Edge, bottom.Size));
                var ownChanges = Changes(own);
                var topChanges = Changes(top);
                var bottomChanges = Changes(bottom);

                File.WriteAllText(path, $"clock24 = true\n\n[{bar}]\nedge = \"left\"\n\n[{dock}]\nedge = \"bottom\"\nsize = 64\n");
                await Wait.UntilAsync(() => bottom.Size == 64, "the saved size to reach its section");
                Assert.AreSequenceEqual([nameof(BarPreferences.Size)], bottomChanges);
                Assert.IsEmpty(topChanges);
                Assert.IsEmpty(ownChanges);

                File.WriteAllText(path, $"clock24 = true\n\n[{bar}]\n\n[{dock}]\nedge = \"bottom\"\nsize = 64\n");
                await Wait.UntilAsync(() => top.Edge == "top", "the removed edge to go back to its default");
                Assert.AreSequenceEqual([nameof(BarPreferences.Edge)], topChanges);
                Assert.AreSequenceEqual([nameof(BarPreferences.Size)], bottomChanges);
                Assert.IsEmpty(ownChanges);
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        });
    }

    [TestMethod]
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

                Assert.AreEqual($"# mine\nclock24 = false\n\n[{bar}]\n# the edge\nedge = \"left\"\nother = 1\n",
                    File.ReadAllText(path));
                await NextTurn(shell);
                Assert.AreEqual($"# mine\nclock24 = true\n\n[{bar}]\n# the edge\nedge = \"left\"\nother = 1\nsize = 40\n",
                    File.ReadAllText(path));
                Assert.AreEqual("left", top.Edge);

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

    [TestMethod]
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
                Assert.AreEqual(
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

    [TestMethod]
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
                Assert.AreEqual(
                    $"# mine\nclock24 = true\n\n[{bar}]\n\n# top, bottom, left or right.\n# edge = \"top\"\n\n# size = 32\n",
                    File.ReadAllText(path));

                var logLength = ShellFixture.ReadLog(shell).Length;
                var unknown = "unknown" + Guid.NewGuid().ToString("N");
                var edited = $"# mine\nclock24 = false\n\n[{unknown}]\n";
                File.WriteAllText(path, edited);
                await Wait.UntilAsync(() => ShellFixture.ReadLog(shell)[logLength..]
                        .Contains($"no component named '{unknown}'", StringComparison.Ordinal),
                    "the hand save to be checked");
                Assert.AreEqual(edited, File.ReadAllText(path));
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        });
    }

    [TestMethod]
    public async Task A_missing_file_is_written_with_each_sections_defaults_commented_out()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PathOf(shell);
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
                using var area = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps
                {
                    Name = name,
                    Defaults = [new(widget, new Dictionary<string, object>
                    {
                        ["text"] = "a \"quoted\" value\nand a line break",
                        ["count"] = 7,
                        ["nested"] = new Dictionary<string, object> { ["enabled"] = true }
                    }), new(widget)]
                });
                using var empty = new Mounted<WidgetArea, WidgetAreaProps>(new WidgetAreaProps { Name = name + "empty" });
                await Wait.UntilAsync(
                    () => File.Exists(path) &&
                          File.ReadAllText(path).Contains($"[{name}empty]", StringComparison.Ordinal),
                    "the missing file to be written with each section");

                var text = File.ReadAllText(path);
                foreach (var line in text.Split('\n').Where(line => line.Length > 0))
                    Assert.IsTrue(line.StartsWith('#') || line.StartsWith('['), line);
                Assert.Contains("# Show 13:00 rather than 1:00 PM.\n# clock24 = false\n", text, StringComparison.Ordinal);
                Assert.DoesNotContain("note", text, StringComparison.Ordinal);
                Assert.Contains("# top, bottom, left or right.\n# edge = \"top\"\n", text, StringComparison.Ordinal);
                var uncommented = string.Join("\n\n", text.Split("\n\n").Skip(1).Select(block =>
                {
                    var lines = block.TrimEnd('\n').Split('\n');
                    if (lines[^1].StartsWith("# ", StringComparison.Ordinal))
                        lines[^1] = lines[^1][2..];
                    return string.Join('\n', lines);
                }));
                Assert.IsTrue(TomlDocument.TryParse(path, uncommented, out var document, out var error), error);
                Assert.AreEqual(false, document.Values["clock24"]);
                var section = Assert.IsInstanceOfType<IReadOnlyDictionary<string, object?>>(document.Values[bar]);
                Assert.AreEqual("top", section["edge"]);
                Assert.AreEqual(32L, section["size"]);
                section = Assert.IsInstanceOfType<IReadOnlyDictionary<string, object?>>(document.Values[name]);
                var rows = Assert.IsInstanceOfType<IReadOnlyList<object?>>(section["widgets"]);
                Assert.AreEqual(2, rows.Count);
                var first = Assert.IsInstanceOfType<IReadOnlyDictionary<string, object?>>(rows[0]);
                Assert.AreEqual(widget, first["widget"]);
                Assert.AreEqual("a \"quoted\" value\nand a line break", first["text"]);
                Assert.AreEqual(7L, first["count"]);
                Assert.AreEqual(true, Assert.IsInstanceOfType<IReadOnlyDictionary<string, object?>>(first["nested"])["enabled"]);
                Assert.AreEqual(widget, Assert.IsInstanceOfType<IReadOnlyDictionary<string, object?>>(rows[1])["widget"]);
                section = Assert.IsInstanceOfType<IReadOnlyDictionary<string, object?>>(document.Values[name + "empty"]);
                Assert.IsEmpty(Assert.IsInstanceOfType<IReadOnlyList<object?>>(section["widgets"]));
                Assert.AreEqual(("top", 32), (top.Edge, top.Size));
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        });
    }

    [TestMethod]
    public async Task A_missing_file_is_written_on_the_turn_after_its_section_is_made()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PathOf(shell);
            if (File.Exists(path))
                File.Delete(path);
            var name = "bar" + Guid.NewGuid().ToString("N");
            BarPreferences? late = null;
            try
            {
                using var own = new Own();
                shell.Loop.Post(() => late = new BarPreferences(name));
                Assert.IsFalse(File.Exists(path));

                await NextTurn(shell);
                Assert.IsTrue(File.Exists(path), "the missing file was not written on the turn after its section was made");
                Assert.Contains("# clock24 = false", File.ReadAllText(path), StringComparison.Ordinal);

                await NextTurn(shell);
                Assert.Contains($"[{name}]", File.ReadAllText(path), StringComparison.Ordinal);
            }
            finally
            {
                late?.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
            }

            return true;
        });
    }

    [TestMethod]
    public async Task A_value_left_unchanged_is_named_once_and_stays_in_the_file_until_it_is_assigned()
    {
        await WithFile(bar => $"[{bar}]\nedge = 3\nsize = 4\n", async (shell, path, bar) =>
        {
            var logLength = ShellFixture.ReadLog(shell).Length;
            using var top = new BarPreferences(bar);
            Assert.AreEqual(("top", 4), (top.Edge, top.Size));

            top.Size = 5;
            await NextTurn(shell);
            Assert.AreEqual($"[{bar}]\nedge = 3\nsize = 5\n", File.ReadAllText(path));

            Wait.ForNothingToHappen(TimeSpan.FromMilliseconds(200));
            await NextTurn(shell);
            var warning = $"{path}[2:1]: edge should be a string, not 3, so it was left unchanged";
            Assert.ContainsSingle(ShellFixture.ReadLog(shell)[logLength..].Split(warning)[1..]);

            top.Edge = "left";
            await NextTurn(shell);
            Assert.AreEqual($"[{bar}]\nedge = \"left\"\nsize = 5\n", File.ReadAllText(path));
        });
    }

    [TestMethod]
    public async Task A_write_starts_from_the_file_on_disk_even_after_a_torn_read()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"le", async (shell, path, bar) =>
        {
            using var top = new BarPreferences(bar);
            Assert.AreEqual("top", top.Edge);

            top.Size = 40;
            File.WriteAllText(path, $"[{bar}]\nedge = \"left\" # kept\nsize = 9\n\n[other]\nkept = true\n");
            await NextTurn(shell);

            Assert.AreEqual($"[{bar}]\nedge = \"left\" # kept\nsize = 40\n\n[other]\nkept = true\n", File.ReadAllText(path));
            Assert.AreEqual(("left", 40), (top.Edge, top.Size));
        });
    }

    [TestMethod]
    public async Task A_file_another_program_holds_is_neither_taken_for_missing_nor_written()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", async (shell, path, bar) =>
        {
            var text = File.ReadAllText(path);
            using var first = new BarPreferences(bar);
            var logLength = ShellFixture.ReadLog(shell).Length;
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using var second = new BarPreferences(bar);
                Assert.AreEqual(("left", "left"), (first.Edge, second.Edge));

                first.Size = 40;
                await NextTurn(shell);
                var log = ShellFixture.ReadLog(shell)[logLength..];
                Assert.Contains("the file was left unchanged", log, StringComparison.Ordinal);
                Assert.DoesNotContain("failed to write", log, StringComparison.Ordinal);
            }

            Assert.AreEqual(text, File.ReadAllText(path));
        });
    }

    [TestMethod]
    public async Task A_byte_order_mark_is_kept_and_a_write_leaves_no_temporary_file()
    {
        await WithFile(_ => "", async (shell, path, bar) =>
        {
            File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes($"[{bar}]\nedge = \"left\"\n")]);
            using var top = new BarPreferences(bar);
            Assert.AreEqual("left", top.Edge);

            top.Size = 40;
            await NextTurn(shell);

            var written = File.ReadAllBytes(path);
            Assert.AreSequenceEqual<byte>([0xEF, 0xBB, 0xBF], written[..3]);
            Assert.AreEqual($"[{bar}]\nedge = \"left\"\nsize = 40\n", Encoding.UTF8.GetString(written[3..]));
            Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".tmp-*"));
        });
    }

    [TestMethod]
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
            Assert.AreEqual(text, File.ReadAllText(path));
            Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".tmp-*"));

            File.SetAttributes(path, FileAttributes.Normal);
            top.Edge = "bottom";
            await NextTurn(shell);
            Assert.AreEqual($"[{bar}]\nedge = \"bottom\"\nsize = 40\n", File.ReadAllText(path));
        });
    }

    [TestMethod]
    public async Task Disposing_a_section_finishes_its_write_on_its_way()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", (_, path, bar) =>
        {
            var top = new BarPreferences(bar);

            top.Size = 40;
            top.Dispose();

            Assert.AreEqual($"[{bar}]\nedge = \"left\"\nsize = 40\n", File.ReadAllText(path));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task A_disposed_section_does_not_follow_or_write()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", async (shell, path, bar) =>
        {
            using var live = new BarPreferences(bar);
            var gone = new BarPreferences(bar);
            gone.Dispose();

            File.WriteAllText(path, $"[{bar}]\nedge = \"bottom\"\n");
            await Wait.UntilAsync(() => live.Edge == "bottom", "the hand save to be read");
            Assert.AreEqual("left", gone.Edge);

            gone.Size = 40;
            live.Edge = "right";
            await NextTurn(shell);
            Assert.AreEqual($"[{bar}]\nedge = \"right\"\n", File.ReadAllText(path));
        });
    }

    [TestMethod]
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
                Assert.AreEqual(path, heard[0].Path);
                Assert.StartsWith(path + "[2:", heard[0].Error, StringComparison.Ordinal);
                Assert.AreEqual("left", top.Edge);

                var logLength = ShellFixture.ReadLog(shell).Length;
                top.Size = 40;
                await NextTurn(shell);
                Assert.AreEqual(broken, File.ReadAllText(path));
                Assert.Contains("the file was left unchanged", ShellFixture.ReadLog(shell)[logLength..],
                    StringComparison.Ordinal);

                File.WriteAllText(path, $"[{bar}]\nedge = \"bottom\"\n");
                await Wait.UntilAsync(() => top.Edge == "bottom", "the fixed file to be read");
                Assert.AreSequenceEqual([(path, heard[0].Error), (path, "")], heard);
                Assert.AreEqual(32, top.Size);
                Assert.AreEqual($"[{bar}]\nedge = \"bottom\"\n", File.ReadAllText(path));
            }
            finally
            {
                stop();
            }
        });
    }

    [TestMethod]
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
                Assert.AreEqual("left", top.Edge);

                top.Size = 40;
                await NextTurn(shell);
                Assert.AreSequenceEqual(oversized, File.ReadAllBytes(path));

                File.WriteAllText(path, text);
                await Wait.UntilAsync(() => heard.Count > 1, "the file back under the limit to be read");
                Assert.AreEqual((path, ""), heard[1]);
            }
            finally
            {
                stop();
            }
        });
    }

    [TestMethod]
    public async Task A_deleted_file_brings_back_the_defaults_and_is_written_again()
    {
        await WithFile(bar => $"[{bar}]\nedge = \"left\"\n", async (_, path, bar) =>
        {
            using var top = new BarPreferences(bar);
            Assert.AreEqual("left", top.Edge);

            File.Delete(path);

            await Wait.UntilAsync(() => File.Exists(path), "the deleted file to be written again");
            Assert.AreEqual("top", top.Edge);
            Assert.Contains($"[{bar}]\n\n# top, bottom, left or right.\n# edge = \"top\"\n", File.ReadAllText(path),
                StringComparison.Ordinal);
        });
    }

    private sealed class DefaultWidget : Component<WidgetContext>
    {
        public override Element Render() => TextBlock("default");
    }
}
