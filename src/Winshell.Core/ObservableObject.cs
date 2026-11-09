using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winshell;

/// <summary>
/// The base of what a service exposes. A change is raised on the thread that made it, which for a service is its
/// event loop, so XAML bindings and Reactor can read it. Each public getter, and each public method that answers from
/// its state, reports its read with <see cref="Read{T}"/>, so a watch that reads it runs again when it changes.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>Raised when a property's value changes, with the property's name.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Reports a read of <paramref name="property"/>, by default the calling property, and returns
    /// <paramref name="value"/>. A getter reports before it computes anything, as in <c>Read(_info).Name</c>.
    /// </summary>
    protected T Read<T>(T value, [CallerMemberName] string property = "")
    {
        return Reads.Property(this, value, property);
    }

    /// <summary>Sets a field and reports the change when the value differs.</summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string property = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Changed(property);
        return true;
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
