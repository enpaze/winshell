using System.ComponentModel;

namespace Winshell;

internal sealed class PropertyListeners(object sender, Action start, Action stop) : IDisposable
{
    private PropertyChangedEventHandler? _handlers;

    public bool Any => _handlers is not null;

    public bool Disposed { get; private set; }

    public void Add(PropertyChangedEventHandler? handler)
    {
        var first = _handlers is null;
        _handlers += handler;
        if (first && _handlers is not null && !Disposed)
            start();
    }

    public void Remove(PropertyChangedEventHandler? handler)
    {
        _handlers -= handler;
        if (_handlers is null)
            stop();
    }

    public void Raise(string property)
    {
        _handlers?.Invoke(sender, new PropertyChangedEventArgs(property));
    }

    public void Dispose()
    {
        if (Disposed)
            return;
        Disposed = true;
        stop();
    }
}
