namespace Winshell.Core.Tests;

[TestClass]
public sealed class TomlLimitsTests
{
    private const int Megabyte = 1024 * 1024;

    private static string Dotted(int segments)
    {
        return string.Join('.', Enumerable.Repeat("a", segments)) + " = 1";
    }

    private static string Nested(int depth)
    {
        return "a = " + new string('[', depth) + "1" + new string(']', depth);
    }

    [TestMethod]
    public void Nesting_past_64_levels_is_refused()
    {
        Assert.IsTrue(TomlDocument.TryParse("deep.toml", Dotted(64), out _, out var error), error);
        Assert.IsFalse(TomlDocument.TryParse("deep.toml", Dotted(65), out _, out _));

        Assert.IsTrue(TomlDocument.TryParse("arrays.toml", Nested(63), out _, out error), error);
        Assert.IsFalse(TomlDocument.TryParse("arrays.toml", Nested(64), out _, out _));

        using var dir = new TempFolder();
        var deep = Path.Combine(dir.Path, "deep.toml");
        var arrays = Path.Combine(dir.Path, "arrays.toml");
        File.WriteAllText(deep, Dotted(65));
        File.WriteAllText(arrays, Nested(64));
        Assert.AreEqual(TomlRead.Invalid, TomlDocument.Load(deep).Kind);
        Assert.AreEqual(TomlRead.Invalid, TomlDocument.Load(arrays).Kind);
    }

    [TestMethod]
    public void Text_past_1_MiB_is_not_parsed()
    {
        var text = "# " + new string('x', Megabyte);

        Assert.IsFalse(TomlDocument.TryParse("big.toml", text, out var document, out var error));
        Assert.IsNull(document);
        Assert.AreEqual("big.toml: file is 1048578 bytes, above the limit of 1048576", error);
    }

    [TestMethod]
    public void The_limit_counts_bytes_and_not_characters()
    {
        var text = "name = \"" + new string('é', Megabyte / 2) + "\"\n";

        Assert.IsTrue(text.Length < Megabyte);
        Assert.IsFalse(TomlDocument.TryParse("accented.toml", text, out _, out var error));
        Assert.AreEqual("accented.toml: file is 1048586 bytes, above the limit of 1048576", error);
    }

    [TestMethod]
    public void Loading_a_file_names_what_went_wrong()
    {
        var dir = Directory.CreateDirectory(TempFolder.NewPath());
        var missing = Path.Combine(dir.FullName, "missing.toml");
        var bad = Path.Combine(dir.FullName, "bad.toml");
        var big = Path.Combine(dir.FullName, "big.toml");
        var marked = Path.Combine(dir.FullName, "marked.toml");
        var plain = Path.Combine(dir.FullName, "plain.toml");
        try
        {
            Assert.AreEqual(TomlRead.Missing, TomlDocument.Load(missing).Kind);

            File.WriteAllText(bad, "name =\n");
            var invalid = TomlDocument.Load(bad);
            Assert.AreEqual(TomlRead.Invalid, invalid.Kind);
            Assert.AreEqual(
                $"{bad}[1:7]: Unexpected token end-of-line found while expecting a value",
                invalid.Describe(bad));

            File.WriteAllBytes(big, new byte[Megabyte + 1]);
            var tooLarge = TomlDocument.Load(big);
            Assert.AreEqual(TomlRead.TooLarge, tooLarge.Kind);
            Assert.AreEqual($"{big}: file is 1048577 bytes, above the limit of 1048576", tooLarge.Describe(big));

            using (new FileStream(bad, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var blocked = TomlDocument.Load(bad);
                Assert.AreEqual(TomlRead.Unreadable, blocked.Kind);
                Assert.AreEqual(0, blocked.Line);
                Assert.AreEqual(0, blocked.Column);
                Assert.Contains("bad.toml", blocked.Message, StringComparison.Ordinal);
                Assert.AreEqual($"{bad}: {blocked.Message}", blocked.Describe(bad));
            }

            var withMark = new byte[] { 0xEF, 0xBB, 0xBF };
            File.WriteAllBytes(marked, [.. withMark, .. "name = \"mine\"\n"u8]);
            var parsed = TomlDocument.Load(marked);
            Assert.AreEqual(TomlRead.Parsed, parsed.Kind);
            Assert.IsTrue(parsed.Bom);
            Assert.AreEqual("mine", parsed.Document!.Values["name"]);

            File.WriteAllBytes(plain, "name = \"mine\"\n"u8.ToArray());
            var withoutMark = TomlDocument.Load(plain);
            Assert.AreEqual(TomlRead.Parsed, withoutMark.Kind);
            Assert.IsFalse(withoutMark.Bom);
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
