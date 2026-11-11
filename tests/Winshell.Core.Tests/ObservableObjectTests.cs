using System.ComponentModel;

namespace Winshell.Core.Tests;

/// <summary>
/// Reading a list property follows that list. Reactor's tree walker hears the object's
/// <see cref="INotifyPropertyChanged.PropertyChanged"/>, not a tracked read, so the follow has to stay when the read
/// report goes.
/// </summary>
public sealed class ObservableObjectTests
{
    [Fact]
    public void Reading_a_list_property_raises_the_object_when_an_item_changes()
    {
        var shelf = new Shelf();
        var item = new Reading { Level = 1 };
        shelf.Items.Add(item);
        var names = new List<string?>();
        shelf.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        item.Level = 2;

        Assert.Equal(["Items"], names);
    }

    [Fact]
    public void Following_one_list_under_a_second_property_throws()
    {
        var shelf = new SharedShelf();
        var item = new Reading { Level = 1 };
        shelf.First.Add(item);
        _ = shelf.First;

        var error = Assert.Throws<InvalidOperationException>(() => _ = shelf.Second);

        Assert.Contains(nameof(SharedShelf.First), error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SharedShelf.Second), error.Message, StringComparison.Ordinal);
        var names = new List<string?>();
        shelf.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        item.Level = 2;
        Assert.Equal([nameof(SharedShelf.First)], names);
    }

    /// <summary>A service whose list is created with <c>= []</c>, so the getter is what starts the follow.</summary>
    private sealed class Shelf : ObservableObject
    {
        public ObservableList<Reading> Items { get => Read(field); } = [];
    }

    /// <summary>Two properties that return one list. The second follow must fail instead of staying silent.</summary>
    private sealed class SharedShelf : ObservableObject
    {
        private readonly ObservableList<Reading> _items = [];

        public ObservableList<Reading> First => Read(_items);

        public ObservableList<Reading> Second => Read(_items);
    }

    /// <summary>An item in <see cref="Shelf.Items"/>.</summary>
    private sealed class Reading : ObservableObject
    {
        public int Level
        {
            get => field;
            set => Set(ref field, value);
        }
    }
}
