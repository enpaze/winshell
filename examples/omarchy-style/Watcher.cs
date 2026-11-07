using System.Collections.Specialized;
using System.ComponentModel;

namespace OmarchyStyle;

/// <summary>
/// Runs a render again whenever an object or list it read through <see cref="Read{T}"/> last time changes, so a
/// control shows what a service holds now without a subscription per property. Services raise their changes on the
/// shell's thread, which is the panel's.
/// </summary>
internal sealed class Watcher : IDisposable
{
    private readonly Action<Watcher> _render;
    private readonly HashSet<INotifyPropertyChanged> _objects = [];
    private readonly HashSet<INotifyCollectionChanged> _lists = [];
    private bool _disposed;

    public Watcher(Action<Watcher> render)
    {
        _render = render;
        Render();
    }

    /// <summary>Returns <paramref name="source"/>, and renders again when it changes: a list when its items do.</summary>
    public T Read<T>(T source) where T : INotifyPropertyChanged
    {
        if (source is INotifyCollectionChanged list)
        {
            if (_lists.Add(list))
                list.CollectionChanged += OnChanged;
        }
        else if (_objects.Add(source))
        {
            source.PropertyChanged += OnChanged;
        }

        return source;
    }

    public void Dispose()
    {
        _disposed = true;
        Forget();
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        Render();
    }

    private void Render()
    {
        // An event already being raised still reaches a watcher that has just been disposed.
        if (_disposed)
            return;
        Forget();
        _render(this);
    }

    private void Forget()
    {
        foreach (var source in _objects)
            source.PropertyChanged -= OnChanged;
        foreach (var list in _lists)
            list.CollectionChanged -= OnChanged;
        _objects.Clear();
        _lists.Clear();
    }
}
