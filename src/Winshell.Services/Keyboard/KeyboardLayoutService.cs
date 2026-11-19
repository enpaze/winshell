using System.ComponentModel;

namespace Winshell.Keyboard;

public sealed class KeyboardLayoutService : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    private readonly IEventLoop _loop;
    private readonly IKeyboardLayoutBackend _backend;
    private readonly PropertyListeners _listeners;
    private IDisposable? _timer;
    private KeyboardSnapshot? _last;
    private IReadOnlyList<KeyboardLayout> _layouts = [];
    private KeyboardLayout? _current;

    internal KeyboardLayoutService(IEventLoop loop, IKeyboardLayoutBackend? backend = null)
    {
        _loop = loop;
        _backend = backend ?? new KeyboardLayoutBackend();
        _listeners = new PropertyListeners(this, Sample, Stop);
    }

    public IReadOnlyList<KeyboardLayout> Layouts => _layouts;

    public KeyboardLayout? Current => _current;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _listeners.Add(value);
        remove => _listeners.Remove(value);
    }

    public bool Use(KeyboardLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (_listeners.Disposed)
            return false;
        var snapshot = Snapshot();
        if (!Listed(snapshot, layout.Id) || snapshot.CurrentId == layout.Id)
            return false;
        return _backend.Use(layout.Id);
    }

    public bool Next()
    {
        if (_listeners.Disposed)
            return false;
        var snapshot = Snapshot();
        if (snapshot.Layouts.Count == 0)
            return false;
        var index = -1;
        for (var i = 0; i < snapshot.Layouts.Count; i++)
            if (snapshot.Layouts[i].Id == snapshot.CurrentId)
                index = i;
        var next = snapshot.Layouts[(index + 1) % snapshot.Layouts.Count];
        if (next.Id == snapshot.CurrentId)
            return false;
        return _backend.Use(next.Id);
    }

    void IDisposable.Dispose()
    {
        _listeners.Dispose();
    }

    private void Sample()
    {
        _timer = _loop.Schedule(Interval, Sample);
        Publish(_backend.Read());
    }

    private void Publish(KeyboardSnapshot snapshot)
    {
        var layouts = new KeyboardLayout[snapshot.Layouts.Count];
        KeyboardLayout? current = null;
        for (var i = 0; i < layouts.Length; i++)
        {
            var layout = snapshot.Layouts[i];
            layouts[i] = new KeyboardLayout(layout.Id, Label(layout.Language, layout.Name), layout.Name);
            if (layout.Id == snapshot.CurrentId)
                current = layouts[i];
        }

        var layoutsChanged = !Same(_layouts, layouts);
        var currentChanged = !Same(_current, current);
        _layouts = layouts;
        _current = current;
        _last = snapshot;
        if (layoutsChanged)
            _listeners.Raise(nameof(Layouts));
        if (currentChanged)
            _listeners.Raise(nameof(Current));
    }

    private void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _last = null;
        _layouts = [];
        _current = null;
    }

    private KeyboardSnapshot Snapshot()
    {
        return !_listeners.Any || _last is null ? _backend.Read() : _last;
    }

    private static bool Listed(KeyboardSnapshot snapshot, string id)
    {
        foreach (var layout in snapshot.Layouts)
            if (layout.Id == id)
                return true;
        return false;
    }

    private static string Label(string language, string name)
    {
        var source = language.Length > 0 ? language : FirstWord(name);
        var dash = source.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
            source = source[..dash];
        if (source.Length > 3)
            source = source[..3];
        return source.ToUpperInvariant();
    }

    private static string FirstWord(string name)
    {
        var space = name.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? name : name[..space];
    }

    private static bool Same(IReadOnlyList<KeyboardLayout> left, IReadOnlyList<KeyboardLayout> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var i = 0; i < left.Count; i++)
            if (!Same(left[i], right[i]))
                return false;
        return true;
    }

    private static bool Same(KeyboardLayout? left, KeyboardLayout? right)
    {
        return left is null
            ? right is null
            : right is not null && left.Id == right.Id && left.Label == right.Label && left.Name == right.Name;
    }
}
