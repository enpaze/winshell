namespace Winshell.ShellTests;

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

        var written = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            Assert.Single(Placement.Format([placement])));
        Assert.Equal(["widget", "text", "count", "options"], written.Keys);
        Assert.Equal("clock", written["widget"]);
        Assert.Equal("Hello", written["text"]);
        Assert.Equal(3L, written["count"]);
        Assert.Equal(true,
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(written["options"]!)["enabled"]);
    }

    [Fact]
    public void A_list_with_an_item_that_names_no_widget_is_refused_and_nothing_else_is_a_list_of_placements()
    {
        const string refused = "must be a list of widget names or tables that each name a widget";
        var named = new Dictionary<string, object?>[]
        {
            new() { ["widget"] = "clock" },
            new() { ["widget"] = 42L }
        };
        var blank = new Dictionary<string, object?>[] { new() { ["widget"] = "  " } };
        var missing = new Dictionary<string, object?>[] { new() { ["text"] = "Hello" } };
        Assert.Equal(refused, Placement.Check(named));
        Assert.Equal(refused, Placement.Check(blank));
        Assert.Equal(refused, Placement.Check(missing));
        Assert.StartsWith(refused,
            Assert.Throws<ArgumentException>(() => Placement.Parse(named)).Message, StringComparison.Ordinal);

        Assert.Equal(refused, Placement.Check(new object?[] { "clock", 42L }));
        Assert.Equal(refused, Placement.Check(new object?[] { "clock", new Dictionary<string, object?> { ["text"] = "Hi" } }));
        Assert.Equal(refused, Placement.Check(new object?[] { "clock", " " }));
        Assert.Equal(refused, Placement.Check(new object?[] { "clock", null }));
        Assert.Equal(refused, Placement.Check(new object?[] { new object?[] { "clock" } }));
        Assert.StartsWith(refused,
            Assert.Throws<ArgumentException>(() => Placement.Parse(new object?[] { "clock", 42L })).Message,
            StringComparison.Ordinal);

        Assert.Equal(refused, Placement.Check("clock"));
        Assert.Equal(refused, Placement.Check(42L));
        Assert.Equal(refused, Placement.Check(new HashSet<string> { "clock" }));
        Assert.Equal(refused, Placement.Check(new HashSet<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["widget"] = "clock" }
        }));
        Assert.StartsWith(refused,
            Assert.Throws<ArgumentException>(() => Placement.Parse(new HashSet<string> { "clock" })).Message,
            StringComparison.Ordinal);
        Assert.StartsWith(refused, Assert.Throws<ArgumentException>(() => Placement.Parse(
            new HashSet<IReadOnlyDictionary<string, object?>> { new Dictionary<string, object?> { ["widget"] = "clock" } }
        )).Message, StringComparison.Ordinal);
        Assert.Equal(refused, Placement.Check(new OnlySet<string>(["clock"])));
        Assert.Equal(refused, Placement.Check(new OnlyReadOnlySet<string>(["clock"])));
        Assert.Equal(refused, Placement.Check(new Dictionary<string, object?>
        {
            ["widget"] = "clock"
        }));
        Assert.Throws<ArgumentNullException>(() => Placement.Parse(null!));
    }

    [Fact]
    public void A_bare_name_reads_as_a_placement_with_no_settings_in_order_among_tables()
    {
        var items = new List<object?>
        {
            "tray",
            new Dictionary<string, object?> { ["widget"] = "message", ["text"] = "Hi" },
            "clock"
        };

        Assert.Equal("", Placement.Check(items));
        var placements = Placement.Parse(items);
        Assert.Equal(["tray", "message", "clock"], placements.Select(placement => placement.Widget));
        Assert.NotNull(placements[0].Settings);
        Assert.Empty(placements[0].Settings!);
        Assert.Equal(["text"], placements[1].Settings!.Keys);
        Assert.Equal("Hi", placements[1].Settings!["text"]);
        Assert.Empty(placements[2].Settings!);
    }

    [Fact]
    public void A_typed_list_of_tables_or_of_names_is_read_in_order()
    {
        var tables = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["widget"] = "message", ["text"] = "Hi" },
            new Dictionary<string, object?> { ["widget"] = "clock" }
        };
        Assert.Equal("", Placement.Check(tables));
        var read = Placement.Parse(tables);
        Assert.Equal(["message", "clock"], read.Select(placement => placement.Widget));
        Assert.Equal("Hi", read[0].Settings!["text"]);
        Assert.Empty(read[1].Settings!);

        var names = new[] { "tray", "clock" };
        Assert.Equal("", Placement.Check(names));
        Assert.Equal(["tray", "clock"], Placement.Parse(names).Select(placement => placement.Widget));
    }

    [Fact]
    public void A_list_that_is_only_an_IList_of_tables_is_read_in_order()
    {
        var tables = new OnlyList<IReadOnlyDictionary<string, object?>>(
        [
            new Dictionary<string, object?> { ["widget"] = "clock", ["text"] = "Hi" },
            new Dictionary<string, object?> { ["widget"] = "tray" }
        ]);

        Assert.Equal("", Placement.Check(tables));
        var read = Placement.Parse(tables);
        Assert.Equal(["clock", "tray"], read.Select(placement => placement.Widget));
        Assert.Equal("Hi", read[0].Settings!["text"]);
        Assert.Empty(read[1].Settings!);
    }

    [Fact]
    public void An_empty_list_and_a_table_with_only_a_name_are_read_and_a_placement_without_settings_writes_its_bare_name()
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
            new Placement("clock", new Dictionary<string, object> { ["widget"] = "other", ["text"] = "Hi" }),
            new Placement("tray", new Dictionary<string, object>()),
            new Placement("tray", new Dictionary<string, object> { ["widget"] = "other" })
        ]);
        Assert.Equal("clock", written[0]);
        var table = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(written[1]);
        Assert.Equal("clock", table["widget"]);
        Assert.Equal("Hi", table["text"]);
        Assert.Equal(2, table.Count);
        Assert.Equal("tray", written[2]);
        Assert.Equal("tray", written[3]);
        Assert.Equal("[\"clock\", { widget = \"clock\", text = \"Hi\" }, \"tray\", \"tray\"]", TomlDocument.Format(written));
    }

    private sealed class OnlyList<T>(List<T> items) : IList<T>
    {
        public T this[int index] { get => items[index]; set => items[index] = value; }
        public int Count => items.Count;
        public bool IsReadOnly => false;
        public int IndexOf(T item) => items.IndexOf(item);
        public void Insert(int index, T item) => items.Insert(index, item);
        public void RemoveAt(int index) => items.RemoveAt(index);
        public void Add(T item) => items.Add(item);
        public void Clear() => items.Clear();
        public bool Contains(T item) => items.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
        public bool Remove(T item) => items.Remove(item);
        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class OnlySet<T>(HashSet<T> items) : ISet<T>
    {
        public int Count => items.Count;
        public bool IsReadOnly => false;
        public bool Add(T item) => items.Add(item);
        void ICollection<T>.Add(T item) => items.Add(item);
        public void Clear() => items.Clear();
        public bool Contains(T item) => items.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
        public bool Remove(T item) => items.Remove(item);
        public void ExceptWith(IEnumerable<T> other) => items.ExceptWith(other);
        public void IntersectWith(IEnumerable<T> other) => items.IntersectWith(other);
        public void SymmetricExceptWith(IEnumerable<T> other) => items.SymmetricExceptWith(other);
        public void UnionWith(IEnumerable<T> other) => items.UnionWith(other);
        public bool IsProperSubsetOf(IEnumerable<T> other) => items.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => items.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => items.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => items.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => items.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => items.SetEquals(other);
        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class OnlyReadOnlySet<T>(HashSet<T> items) : IReadOnlySet<T>
    {
        public int Count => items.Count;
        public bool Contains(T item) => items.Contains(item);
        public bool IsProperSubsetOf(IEnumerable<T> other) => items.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => items.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => items.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => items.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => items.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => items.SetEquals(other);
        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
