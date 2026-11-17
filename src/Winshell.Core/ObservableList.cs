using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Winshell;

public sealed class ObservableList<T> : IReadOnlyList<T>, IList, INotifyCollectionChanged, INotifyPropertyChanged
{
    internal const string ItemsChanged = "Item[]";

    private readonly ObservableCollection<T> _items;
    private readonly Dictionary<INotifyPropertyChanged, int> _followed = new(ReferenceEqualityComparer.Instance);

    internal ObservableList()
        : this([])
    {
    }

    internal ObservableList(IEnumerable<T> items)
    {
        _items = new ObservableCollection<T>(items);
        _items.CollectionChanged += OnCollectionChanged;
        ((INotifyPropertyChanged)_items).PropertyChanged += (_, e) => PropertyChanged?.Invoke(this, e);
        Follow(_items);
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Count => _items.Count;

    public T this[int index]
    {
        get => _items[index];
        internal set => _items[index] = value;
    }

    public int IndexOf(T item)
    {
        return _items.IndexOf(item);
    }

    public bool Contains(T item)
    {
        return _items.Contains(item);
    }

    public IEnumerator<T> GetEnumerator()
    {
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

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                Follow(e.NewItems);
                break;
            case NotifyCollectionChangedAction.Remove:
                Unfollow(e.OldItems);
                break;
            case NotifyCollectionChangedAction.Replace:
                Unfollow(e.OldItems);
                Follow(e.NewItems);
                break;
            case NotifyCollectionChangedAction.Reset:
                UnfollowAll();
                Follow(_items);
                break;
        }

        CollectionChanged?.Invoke(this, e);
    }

    private void Follow(IEnumerable? items)
    {
        if (items is null)
            return;
        foreach (var item in items)
            Follow(item);
    }

    private void Follow(object? item)
    {
        if (item is not INotifyPropertyChanged source || source.GetType().IsValueType)
            return;
        if (_followed.TryGetValue(source, out var count))
        {
            _followed[source] = count + 1;
            return;
        }

        source.PropertyChanged += OnItemChanged;
        _followed[source] = 1;
    }

    private void Unfollow(IEnumerable? items)
    {
        if (items is null)
            return;
        foreach (var item in items)
            Unfollow(item);
    }

    private void Unfollow(object? item)
    {
        if (item is not INotifyPropertyChanged source || !_followed.TryGetValue(source, out var count))
            return;
        if (count > 1)
        {
            _followed[source] = count - 1;
            return;
        }

        source.PropertyChanged -= OnItemChanged;
        _followed.Remove(source);
    }

    private void UnfollowAll()
    {
        foreach (var source in _followed.Keys)
            source.PropertyChanged -= OnItemChanged;
        _followed.Clear();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(ItemsChanged));
    }
}
