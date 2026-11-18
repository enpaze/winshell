using System.ComponentModel;

namespace Winshell.Core.Tests;

[TestClass]
public sealed class ObservableObjectTests
{
    [TestMethod]
    public void Reading_a_list_property_raises_the_object_when_an_item_changes()
    {
        var shelf = new Shelf();
        var item = new Reading { Level = 1 };
        shelf.Items.Add(item);
        var names = new List<string?>();
        shelf.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        item.Level = 2;

        Assert.AreSequenceEqual(["Items"], names);
    }

    [TestMethod]
    public void Following_one_list_under_a_second_property_throws()
    {
        var shelf = new SharedShelf();
        var item = new Reading { Level = 1 };
        shelf.First.Add(item);
        _ = shelf.First;

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => _ = shelf.Second);

        Assert.Contains(nameof(SharedShelf.First), error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SharedShelf.Second), error.Message, StringComparison.Ordinal);
        var names = new List<string?>();
        shelf.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        item.Level = 2;
        Assert.AreSequenceEqual([nameof(SharedShelf.First)], names);
    }

    private sealed class Shelf : ObservableObject
    {
        public ObservableList<Reading> Items { get => Read(field); } = [];
    }

    private sealed class SharedShelf : ObservableObject
    {
        private readonly ObservableList<Reading> _items = [];

        public ObservableList<Reading> First => Read(_items);

        public ObservableList<Reading> Second => Read(_items);
    }

    private sealed class Reading : ObservableObject
    {
        public int Level
        {
            get => field;
            set => Set(ref field, value);
        }
    }
}
