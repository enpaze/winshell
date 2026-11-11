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

    /// <summary>A service whose list is created with <c>= []</c>, so the getter is what starts the follow.</summary>
    private sealed class Shelf : ObservableObject
    {
        public ObservableList<Reading> Items { get => Read(field); } = [];
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
