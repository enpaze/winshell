namespace Winshell.Core.Tests;

/// <summary>
/// The budgets for a TOML file. The numbers are the production limits; a file past one is as large as the limit.
/// </summary>
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

    [Fact]
    public void Nesting_past_64_levels_is_refused()
    {
        Assert.True(TomlDocument.TryParse("deep.toml", Dotted(64), out _, out var error), error);
        Assert.False(TomlDocument.TryParse("deep.toml", Dotted(65), out _, out error));
        Assert.Contains("maximum depth of 64", error, StringComparison.Ordinal);

        Assert.True(TomlDocument.TryParse("arrays.toml", Nested(63), out _, out error), error);
        Assert.False(TomlDocument.TryParse("arrays.toml", Nested(64), out _, out error));
        Assert.Contains("maximum depth of 64", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_past_1_MiB_is_not_parsed()
    {
        var text = "# " + new string('x', Megabyte);

        Assert.False(TomlDocument.TryParse("big.toml", text, out var document, out var error));
        Assert.Null(document);
        Assert.Contains("big.toml: file is ", error, StringComparison.Ordinal);
        Assert.Contains("above the limit of 1048576", error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_limit_counts_bytes_and_not_characters()
    {
        // Fewer characters than the limit, more bytes.
        var text = "name = \"" + new string('é', Megabyte / 2) + "\"\n";

        Assert.False(TomlDocument.TryParse("accented.toml", text, out _, out var error));
        Assert.Contains("above the limit of 1048576", error, StringComparison.Ordinal);
    }

    [Fact]
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
            Assert.Equal(TomlRead.Missing, TomlDocument.Load(missing).Kind);

            File.WriteAllText(bad, "name =\n");
            var invalid = TomlDocument.Load(bad);
            Assert.Equal(TomlRead.Invalid, invalid.Kind);
            Assert.Equal(
                $"{bad}[1:7]: Unexpected token end-of-line found while expecting a value",
                invalid.Describe(bad));

            File.WriteAllBytes(big, new byte[Megabyte + 1]);
            var tooLarge = TomlDocument.Load(big);
            Assert.Equal(TomlRead.TooLarge, tooLarge.Kind);
            Assert.Equal($"{big}: file is 1048577 bytes, above the limit of 1048576", tooLarge.Describe(big));

            using (new FileStream(bad, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var blocked = TomlDocument.Load(bad);
                Assert.Equal(TomlRead.Unreadable, blocked.Kind);
                Assert.Equal(0, blocked.Line);
                Assert.Equal(0, blocked.Column);
                Assert.Contains("bad.toml", blocked.Message, StringComparison.Ordinal);
                Assert.Equal($"{bad}: {blocked.Message}", blocked.Describe(bad));
            }

            var withMark = new byte[] { 0xEF, 0xBB, 0xBF };
            File.WriteAllBytes(marked, [.. withMark, .. "name = \"mine\"\n"u8]);
            var parsed = TomlDocument.Load(marked);
            Assert.Equal(TomlRead.Parsed, parsed.Kind);
            Assert.True(parsed.Bom);
            Assert.Equal("mine", parsed.Document!.Values["name"]);

            File.WriteAllBytes(plain, "name = \"mine\"\n"u8.ToArray());
            var withoutMark = TomlDocument.Load(plain);
            Assert.Equal(TomlRead.Parsed, withoutMark.Kind);
            Assert.False(withoutMark.Bom);
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
