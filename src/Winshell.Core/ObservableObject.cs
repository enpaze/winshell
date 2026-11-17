using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winshell;

public abstract class ObservableObject : INotifyPropertyChanged
{
    private readonly object _listGate = new();
    private Dictionary<INotifyPropertyChanged, string>? _listProperties;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected T Read<T>(T value, [CallerMemberName] string property = "")
    {
        Follow(value, property);
        return value;
    }

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

    private void Follow(object? value, string property)
    {
        if (value is not INotifyPropertyChanged list || value is not INotifyCollectionChanged)
            return;
        lock (_listGate)
        {
            _listProperties ??= new Dictionary<INotifyPropertyChanged, string>(ReferenceEqualityComparer.Instance);
            if (!_listProperties.TryAdd(list, property))
            {
                if (_listProperties[list] == property)
                    return;
                throw new InvalidOperationException(
                    $"The list is already followed as {_listProperties[list]}, so {property} would not notify.");
            }

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

    protected void Changed([CallerMemberName] string property = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    protected void Changed(bool changed, string property)
    {
        if (changed)
            Changed(property);
    }
}
