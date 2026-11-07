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
    private readonly List<INotifyPropertyChanged> _objects = [];
    private readonly List<INotifyCollectionChanged> _lists = [];

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
            list.CollectionChanged += OnChanged;
            _lists.Add(list);
        }
        else
        {
            source.PropertyChanged += OnChanged;
            _objects.Add(source);
        }

        return source;
    }

    public void Dispose()
    {
        foreach (var source in _objects)
            source.PropertyChanged -= OnChanged;
        foreach (var list in _lists)
            list.CollectionChanged -= OnChanged;
        _objects.Clear();
        _lists.Clear();
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        Render();
    }

    private void Render()
    {
        Dispose();
        _render(this);
    }
}
