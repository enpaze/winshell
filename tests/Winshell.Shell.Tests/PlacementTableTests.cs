namespace Winshell.ShellTests;

/// <summary>
/// A placement table is <c>{ widget, ...settings }</c>. <see cref="Placement"/> reads and writes that table, which a
/// bar and a widget area both store as a list.
/// </summary>
public class PlacementTableTests
{
    [Fact]
    public void A_table_reads_as_the_widget_it_names_and_writes_back_with_its_settings()
    {
        var tables = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["widget"] = "clock",
                ["text"] = "Hello",
                ["count"] = 3L,
                ["options"] = new Dictionary<string, object?> { ["enabled"] = true }
            }
        };

        Assert.Equal("", Placement.Check(tables));
        var placement = Assert.Single(Placement.Parse(tables));
        Assert.Equal("clock", placement.Widget);
        Assert.Equal("Hello", placement.Settings!["text"]);
        Assert.Equal(3L, placement.Settings["count"]);
        Assert.Equal(true,
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(placement.Settings["options"])["enabled"]);
        Assert.False(placement.Settings.ContainsKey("widget"));

        var written = Assert.Single(Placement.Format([placement]));
        Assert.Equal(["widget", "text", "count", "options"], written.Keys);
        Assert.Equal("clock", written["widget"]);
        Assert.Equal("Hello", written["text"]);
        Assert.Equal(3L, written["count"]);
        Assert.Equal(true,
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(written["options"]!)["enabled"]);
    }

    [Fact]
    public void A_list_of_tables_that_misses_a_widget_name_is_refused_and_nothing_else_is_a_list_of_tables()
    {
        var named = new Dictionary<string, object?>[]
        {
            new() { ["widget"] = "clock" },
            new() { ["widget"] = 42L }
        };
        var blank = new Dictionary<string, object?>[] { new() { ["widget"] = "  " } };
        var missing = new Dictionary<string, object?>[] { new() { ["text"] = "Hello" } };
        Assert.Equal("must name a widget in each of its tables", Placement.Check(named));
        Assert.Equal("must name a widget in each of its tables", Placement.Check(blank));
        Assert.Equal("must name a widget in each of its tables", Placement.Check(missing));
        Assert.StartsWith("must name a widget in each of its tables",
            Assert.Throws<ArgumentException>(() => Placement.Parse(named)).Message, StringComparison.Ordinal);

        Assert.Equal("must be a list of tables that each name a widget", Placement.Check("clock"));
        Assert.Equal("must be a list of tables that each name a widget", Placement.Check(new Dictionary<string, object?>
        {
            ["widget"] = "clock"
        }));
        Assert.Equal("must be a list of tables that each name a widget", Placement.Check(new object?[] { "clock", null }));
        Assert.Throws<ArgumentNullException>(() => Placement.Parse(null!));
    }

    [Fact]
    public void An_empty_list_and_a_table_with_only_a_name_are_read_and_a_coded_placement_writes_its_own_name()
    {
        Assert.Equal("", Placement.Check(Array.Empty<IReadOnlyDictionary<string, object?>>()));
        Assert.Empty(Placement.Parse(Array.Empty<IReadOnlyDictionary<string, object?>>()));

        var onlyName = Assert.Single(Placement.Parse(new object?[]
        {
            new Dictionary<string, object?> { ["widget"] = "tray" }
        }));
        Assert.NotNull(onlyName.Settings);
        Assert.Empty(onlyName.Settings);
        var keptNull = Assert.Single(Placement.Parse(new object?[]
        {
            new Dictionary<string, object?> { ["widget"] = "tray", ["gone"] = null }
        }));
        Assert.Equal(["gone"], keptNull.Settings!.Keys);
        Assert.Null(keptNull.Settings["gone"]);

        var written = Placement.Format(
        [
            new Placement("clock"),
            new Placement("clock", new Dictionary<string, object> { ["widget"] = "other", ["text"] = "Hi" })
        ]);
        Assert.Equal("clock", written[0]["widget"]);
        Assert.Single(written[0]);
        Assert.Equal("clock", written[1]["widget"]);
        Assert.Equal("Hi", written[1]["text"]);
        Assert.Equal(2, written[1].Count);
    }
}
