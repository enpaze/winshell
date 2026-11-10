using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winshell;

/// <summary>
/// The base of what a service exposes. A change is raised on the thread that made it, which for a service is its
/// event loop, so XAML bindings and Reactor can read it. Each public getter, and each public method that answers from
/// its state, reports its read with <see cref="Read{T}"/>, so a watch that reads it runs again when it changes. A
/// list property, once read, raises this object when that list changes, so a change inside the list reaches the object.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    private readonly object _listGate = new();
    private Dictionary<INotifyPropertyChanged, string>? _listProperties;

    /// <summary>Raised when a property's value changes, with the property's name.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Reports a read of <paramref name="property"/>, by default the calling property, and returns
    /// <paramref name="value"/>. A getter reports before it computes anything, as in <c>Read(_info).Name</c>.
    /// </summary>
    protected T Read<T>(T value, [CallerMemberName] string property = "")
    {
        // Follow starts here because a list property is created with `= []`, not Set, and the getter is the
        // first code that sees it. The report on the next line is the watch; deleting that report leaves the follow.
        Follow(value, property);
        return Reads.Property(this, value, property);
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
