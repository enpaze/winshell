namespace Winshell.ShellTests;

[TestClass]
public class PlacementTableTests
{
    [TestMethod]
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

        Assert.AreEqual("", Placement.Check(tables));
        var placement = Assert.ContainsSingle(Placement.Parse(tables));
        Assert.AreEqual("clock", placement.Widget);
        Assert.AreEqual("Hello", placement.Settings!["text"]);
        Assert.AreEqual(3L, placement.Settings["count"]);
        Assert.AreEqual(true,
            Assert.IsInstanceOfType<IReadOnlyDictionary<string, object?>>(placement.Settings["options"])["enabled"]);
        Assert.IsFalse(placement.Settings.ContainsKey("widget"));

        var written = Assert.ContainsSingle(Placement.Format([placement]));
        Assert.AreSequenceEqual(["widget", "text", "count", "options"], written.Keys);
        Assert.AreEqual("clock", written["widget"]);
        Assert.AreEqual("Hello", written["text"]);
        Assert.AreEqual(3L, written["count"]);
        Assert.AreEqual(true,
            Assert.IsInstanceOfType<IReadOnlyDictionary<string, object?>>(written["options"]!)["enabled"]);
    }

    [TestMethod]
    public void A_list_of_tables_that_misses_a_widget_name_is_refused_and_nothing_else_is_a_list_of_tables()
    {
        var named = new Dictionary<string, object?>[]
        {
            new() { ["widget"] = "clock" },
            new() { ["widget"] = 42L }
        };
        var blank = new Dictionary<string, object?>[] { new() { ["widget"] = "  " } };
        var missing = new Dictionary<string, object?>[] { new() { ["text"] = "Hello" } };
        Assert.AreEqual("must name a widget in each of its tables", Placement.Check(named));
        Assert.AreEqual("must name a widget in each of its tables", Placement.Check(blank));
        Assert.AreEqual("must name a widget in each of its tables", Placement.Check(missing));
        Assert.StartsWith("must name a widget in each of its tables",
            Assert.ThrowsExactly<ArgumentException>(() => Placement.Parse(named)).Message, StringComparison.Ordinal);

        Assert.AreEqual("must be a list of tables that each name a widget", Placement.Check("clock"));
        Assert.AreEqual("must be a list of tables that each name a widget", Placement.Check(new Dictionary<string, object?>
        {
            ["widget"] = "clock"
        }));
        Assert.AreEqual("must be a list of tables that each name a widget", Placement.Check(new object?[] { "clock", null }));
        Assert.ThrowsExactly<ArgumentNullException>(() => Placement.Parse(null!));
    }

    [TestMethod]
    public void An_empty_list_and_a_table_with_only_a_name_are_read_and_a_coded_placement_writes_its_own_name()
    {
        Assert.AreEqual("", Placement.Check(Array.Empty<IReadOnlyDictionary<string, object?>>()));
        Assert.IsEmpty(Placement.Parse(Array.Empty<IReadOnlyDictionary<string, object?>>()));

        var onlyName = Assert.ContainsSingle(Placement.Parse(new object?[]
        {
            new Dictionary<string, object?> { ["widget"] = "tray" }
        }));
        Assert.IsNotNull(onlyName.Settings);
        Assert.IsEmpty(onlyName.Settings);
        var keptNull = Assert.ContainsSingle(Placement.Parse(new object?[]
        {
            new Dictionary<string, object?> { ["widget"] = "tray", ["gone"] = null }
        }));
        Assert.AreSequenceEqual(["gone"], keptNull.Settings!.Keys);
        Assert.IsNull(keptNull.Settings["gone"]);

        var written = Placement.Format(
        [
            new Placement("clock"),
            new Placement("clock", new Dictionary<string, object> { ["widget"] = "other", ["text"] = "Hi" })
        ]);
        Assert.AreEqual("clock", written[0]["widget"]);
        Assert.ContainsSingle(written[0]);
        Assert.AreEqual("clock", written[1]["widget"]);
        Assert.AreEqual("Hi", written[1]["text"]);
        Assert.AreEqual(2, written[1].Count);
    }
}
