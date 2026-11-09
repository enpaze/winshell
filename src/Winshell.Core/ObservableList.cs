using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Winshell;

/// <summary>
/// A list a service keeps current. A configuration reads it, and only its service changes it. Enumerating, counting
/// or indexing it is a read a watch follows. A change is raised on the thread that made it, which for a service is its
/// event loop, so XAML bindings can show it.
/// </summary>
public sealed class ObservableList<T> : IReadOnlyList<T>, IList, INotifyCollectionChanged, INotifyPropertyChanged
{
    private readonly ObservableCollection<T> _items;

    internal ObservableList()
        : this([])
    {
    }

    internal ObservableList(IEnumerable<T> items)
    {
        _items = new ObservableCollection<T>(items);
        _items.CollectionChanged += (_, e) => CollectionChanged?.Invoke(this, e);
        ((INotifyPropertyChanged)_items).PropertyChanged += (_, e) => PropertyChanged?.Invoke(this, e);
    }

    /// <summary>Raised when an item is added, removed, moved or replaced.</summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>Raised when <see cref="Count"/> or the items change, for XAML bindings.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>How many items there are.</summary>
    public int Count
    {
        get
        {
            Reads.List(this);
            return _items.Count;
        }
    }

    /// <summary>The item at <paramref name="index"/>.</summary>
    public T this[int index]
    {
        get
        {
            Reads.List(this);
            return _items[index];
        }
        internal set => _items[index] = value;
    }

    /// <summary>Where <paramref name="item"/> is, or -1 when it is not in the list.</summary>
    public int IndexOf(T item)
    {
        Reads.List(this);
        return _items.IndexOf(item);
    }

    /// <summary>Whether <paramref name="item"/> is in the list.</summary>
    public bool Contains(T item)
    {
        Reads.List(this);
        return _items.Contains(item);
    }

    /// <summary>Goes through the items in order.</summary>
    public IEnumerator<T> GetEnumerator()
    {
        Reads.List(this);
        return _items.GetEnumerator();
    }

    internal void Add(T item)
    {
        _items.Add(item);
    }

    internal void Insert(int index, T item)
    {
        _items.Insert(index, item);
    }

    internal bool Remove(T item)
    {
        return _items.Remove(item);
    }

    internal void RemoveAt(int index)
    {
        _items.RemoveAt(index);
    }

    internal void Move(int from, int to)
    {
        _items.Move(from, to);
    }

    internal void Clear()
    {
        _items.Clear();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // A XAML list shows an IList, so the list is one, read-only.
    bool IList.IsReadOnly => true;

    bool IList.IsFixedSize => false;

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException("Only its service changes the list.");
    }

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => ((ICollection)_items).SyncRoot;

    bool IList.Contains(object? value)
    {
        return value is T item && Contains(item);
    }

    int IList.IndexOf(object? value)
    {
        return value is T item ? IndexOf(item) : -1;
    }

    void ICollection.CopyTo(Array array, int index)
    {
        Reads.List(this);
        ((ICollection)_items).CopyTo(array, index);
    }

    int IList.Add(object? value)
    {
        throw new NotSupportedException("Only its service changes the list.");
    }

    void IList.Clear()
    {
        throw new NotSupportedException("Only its service changes the list.");
    }

    void IList.Insert(int index, object? value)
    {
        throw new NotSupportedException("Only its service changes the list.");
    }

    void IList.Remove(object? value)
    {
        throw new NotSupportedException("Only its service changes the list.");
    }

    void IList.RemoveAt(int index)
    {
        throw new NotSupportedException("Only its service changes the list.");
    }
}
