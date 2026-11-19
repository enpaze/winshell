using System.ComponentModel;

namespace Winshell.Core.Tests;

[TestClass]
public sealed class ObservableListTests
{
    [TestMethod]
    public void An_items_property_change_raises_the_list()
    {
        var item = new Reading { Level = 1 };
        var list = new ObservableList<Reading>([item]);
        var names = new List<string?>();
        list.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        item.Level = 2;

        Assert.AreSequenceEqual(["Item[]"], names);
    }

    [TestMethod]
    public void A_removed_replaced_or_cleared_item_no_longer_raises_the_list()
    {
        var item = new Reading();
        var list = new ObservableList<Reading>();
        list.Add(item);
        list.Remove(item);

        Assert.IsEmpty(RaisedBy(list, () => item.Level = 1));

        var replacement = new Reading();
        list.Add(item);
        list[0] = replacement;
        Assert.IsEmpty(RaisedBy(list, () => item.Level = 2));
        Assert.AreSequenceEqual(["Item[]"], RaisedBy(list, () => replacement.Level = 3));

        list.Clear();
        Assert.IsEmpty(RaisedBy(list, () => replacement.Level = 4));

        var initial = new Reading();
        var constructed = new ObservableList<Reading>([initial]);
        constructed.Clear();
        Assert.IsEmpty(RaisedBy(constructed, () => initial.Level = 5));

        var twice = new Reading();
        var copies = new ObservableList<Reading>();
        copies.Add(twice);
        copies.Add(twice);
        copies.Remove(twice);
        Assert.AreSequenceEqual(["Item[]"], RaisedBy(copies, () => twice.Level = 6));
        copies.Remove(twice);
        Assert.IsEmpty(RaisedBy(copies, () => twice.Level = 7));
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

    private sealed class Reading : ObservableObject
    {
        public int Level
        {
            get => field;
            set => Set(ref field, value);
        }
    }
}
