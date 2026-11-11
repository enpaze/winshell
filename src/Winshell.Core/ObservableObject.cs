using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winshell;

/// <summary>
/// The base of what a service exposes. A change is raised on the thread that made it, which for a service is its
/// event loop, so Reactor can read it. A list property, once read through <see cref="Read{T}"/>, raises this object
/// when that list changes, so a change inside the list reaches the object.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    private readonly object _listGate = new();
    private Dictionary<INotifyPropertyChanged, string>? _listProperties;

    /// <summary>Raised when a property's value changes, with the property's name.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Follows <paramref name="value"/> when it is a list, under <paramref name="property"/>, and returns it. A list
    /// property is created with <c>= []</c>, not <see cref="Set{T}"/>, so the getter is the first code that sees it,
    /// as in <c>get => Read(field)</c>.
    /// </summary>
    protected T Read<T>(T value, [CallerMemberName] string property = "")
    {
        Follow(value, property);
        return value;
    }

    /// <summary>Sets a field and reports the change when the value differs.</summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string property = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        Unfollow(field);
        field = value;
        Follow(value, property);
        Changed(property);
        return true;
    }

    // A list raises the property that returned it. The tree walker subscribes to the list and never to its items.
    private void Follow(object? value, string property)
    {
        if (value is not INotifyPropertyChanged list || value is not INotifyCollectionChanged)
            return;
        lock (_listGate)
        {
            _listProperties ??= new Dictionary<INotifyPropertyChanged, string>(ReferenceEqualityComparer.Instance);
            // One list returned by two properties raises the first name only. Replacing either property
            // drops that follow. No service property shares a list with another.
            if (!_listProperties.TryAdd(list, property))
                return;
            list.PropertyChanged += OnListChanged;
        }
    }

    private void Unfollow(object? value)
    {
        if (value is not INotifyPropertyChanged list || value is not INotifyCollectionChanged)
            return;
        lock (_listGate)
        {
            if (_listProperties is null || !_listProperties.Remove(list))
                return;
            list.PropertyChanged -= OnListChanged;
        }
    }

    private void OnListChanged(object? sender, PropertyChangedEventArgs e)
    {
        // One add raises Count and Item[]. Item[] is the change, including an item's own property.
        if (e.PropertyName is not (null or ObservableList<object>.ItemsChanged))
            return;
        string? property;
        lock (_listGate)
        {
            if (_listProperties is null || sender is not INotifyPropertyChanged list ||
                !_listProperties.TryGetValue(list, out property))
                return;
        }

        Changed(property);
    }

    /// <summary>Reports a change of <paramref name="property"/>, by default the calling property.</summary>
    protected void Changed([CallerMemberName] string property = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    /// <summary>Reports a change of a property read from a snapshot, when the two snapshots differ.</summary>
    protected void Changed(bool changed, string property)
    {
        if (changed)
            Changed(property);
    }
}
