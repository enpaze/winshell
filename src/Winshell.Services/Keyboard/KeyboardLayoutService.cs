using System.ComponentModel;

namespace Winshell.Keyboard;

/// <summary>
/// The keyboard layouts Windows has installed. Each has a short label, such as EN, and the name Windows shows.
/// The current layout is the foreground window's, so a shell window that does not take the foreground does not change
/// it. Windows tells only the focused window when a layout changes, so while something listens the foreground window's
/// layout is read twice a second, and not at all while nothing does.
/// </summary>
public sealed class KeyboardLayoutService : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    private readonly IEventLoop _loop;
    private readonly IKeyboardLayoutBackend _backend;
    private PropertyChangedEventHandler? _listeners;
    private IDisposable? _timer;
    private KeyboardSnapshot? _last;
    private IReadOnlyList<KeyboardLayout> _layouts = [];
    private KeyboardLayout? _current;
    private bool _disposed;

    internal KeyboardLayoutService(IEventLoop loop, IKeyboardLayoutBackend? backend = null)
    {
        _loop = loop;
        _backend = backend ?? new KeyboardLayoutBackend();
    }

    /// <summary>The installed layouts, in the order Windows cycles them. Empty while nothing listens.</summary>
    public IReadOnlyList<KeyboardLayout> Layouts => _layouts;

    /// <summary>The layout the foreground window is using, or null when it has none or nothing is listening.</summary>
    public KeyboardLayout? Current => _current;

    /// <summary>
    /// Raised on the loop's thread when <see cref="Layouts"/> or <see cref="Current"/> changes. The first handler
    /// added starts reading Windows, and removing the last stops it. Add and remove handlers on the loop's thread.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            var first = _listeners is null;
            _listeners += value;
            if (first && _listeners is not null && !_disposed)
                Sample();
        }
        remove
        {
            _listeners -= value;
            if (_listeners is null)
                Stop();
        }
    }

    /// <summary>
    /// Asks the foreground window to use <paramref name="layout"/>, as the input indicator does. A layout that is not
    /// installed is ignored, and so is the one already in use. Returns whether the window was asked. Call it on the
    /// loop's thread.
    /// </summary>
    public bool Use(KeyboardLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (_disposed)
            return false;
        var snapshot = Snapshot();
        if (!Listed(snapshot, layout.Id) || snapshot.CurrentId == layout.Id)
            return false;
        return _backend.Use(layout.Id);
    }

    /// <summary>
    /// Asks the foreground window to use the next installed layout, as Win+Space does. Does nothing when that layout
    /// is already in use. Returns whether the window was asked. Call it on the loop's thread.
    /// </summary>
    public bool Next()
    {
        if (_disposed)
            return false;
        var snapshot = Snapshot();
        if (snapshot.Layouts.Count == 0)
            return false;
        // A foreground layout that is not installed starts the cycle at the first one.
        var index = -1;
        for (var i = 0; i < snapshot.Layouts.Count; i++)
            if (snapshot.Layouts[i].Id == snapshot.CurrentId)
                index = i;
        var next = snapshot.Layouts[(index + 1) % snapshot.Layouts.Count];
        if (next.Id == snapshot.CurrentId)
            return false;
        return _backend.Use(next.Id);
    }

    /// <summary>Stops reading Windows, and a handler added later does not start it again.</summary>
    public void Dispose()
    {
        _disposed = true;
        Stop();
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

        // Kept before listeners hear it, so one that stops listening, or throws, cannot leave it stale.
        var layoutsChanged = !Same(_layouts, layouts);
        var currentChanged = !Same(_current, current);
        _layouts = layouts;
        _current = current;
        _last = snapshot;
        if (layoutsChanged)
            _listeners?.Invoke(this, new PropertyChangedEventArgs(nameof(Layouts)));
        if (currentChanged)
            _listeners?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
    }

    private void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _last = null;
        _layouts = [];
        _current = null;
    }

    /// <summary>The last reading while something listens. Otherwise one reading, which does not start the watch.</summary>
    private KeyboardSnapshot Snapshot()
    {
        return _listeners is null || _last is null ? _backend.Read() : _last;
    }

    private static bool Listed(KeyboardSnapshot snapshot, string id)
    {
        foreach (var layout in snapshot.Layouts)
            if (layout.Id == id)
                return true;
        return false;
    }

    /// <summary>
    /// A short label, such as EN: the language code, or the name's first word when Windows reports no code, without a
    /// script suffix, and at most three characters.
    /// </summary>
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
