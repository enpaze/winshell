using System.ComponentModel;

namespace Winshell.Core.Tests;

/// <summary>
/// An <see cref="ObservableList{T}"/> raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>
/// when an item does, which is what Reactor's tree walker hears: it subscribes to the list and does not enumerate it.
/// </summary>
public sealed class ObservableListTests
{
    [Fact]
    public void An_items_property_change_raises_the_list()
    {
        var item = new Reading { Level = 1 };
        var list = new ObservableList<Reading>([item]);
        var names = new List<string?>();
        list.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        item.Level = 2;

        Assert.Equal(["Item[]"], names);
    }

    [Fact]
    public void A_removed_replaced_or_cleared_item_no_longer_raises_the_list()
    {
        var item = new Reading();
        var list = new ObservableList<Reading>();
        list.Add(item);
        list.Remove(item);

        Assert.Empty(RaisedBy(list, () => item.Level = 1));

        var replacement = new Reading();
        list.Add(item);
        list[0] = replacement;
        Assert.Empty(RaisedBy(list, () => item.Level = 2));
        Assert.Equal(["Item[]"], RaisedBy(list, () => replacement.Level = 3));

        list.Clear();
        Assert.Empty(RaisedBy(list, () => replacement.Level = 4));

        var initial = new Reading();
        var constructed = new ObservableList<Reading>([initial]);
        constructed.Clear();
        Assert.Empty(RaisedBy(constructed, () => initial.Level = 5));

        var twice = new Reading();
        var copies = new ObservableList<Reading>();
        copies.Add(twice);
        copies.Add(twice);
        copies.Remove(twice);
        Assert.Equal(["Item[]"], RaisedBy(copies, () => twice.Level = 6));
        copies.Remove(twice);
        Assert.Empty(RaisedBy(copies, () => twice.Level = 7));
    }

    private static List<string?> RaisedBy(ObservableList<Reading> list, Action change)
    {
        var names = new List<string?>();
        PropertyChangedEventHandler onChanged = (_, e) => names.Add(e.PropertyName);
        list.PropertyChanged += onChanged;
        change();
        list.PropertyChanged -= onChanged;
        return names;
    }

    /// <summary>An item a service exposes. Its property is the change a list has to pass on.</summary>
    private sealed class Reading : ObservableObject
    {
        public int Level
        {
            get => field;
            set => Set(ref field, value);
        }
    }
}
