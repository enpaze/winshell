namespace Winshell;

public sealed partial class ScreenWindows : IShellPart
{
    private readonly string _name;
    private readonly Func<ScreenInfo, IReadOnlyDictionary<string, object?>?, IDisposable> _make;
    private readonly Shell _shell;
    private ScreenRows? _rows;

    private readonly Dictionary<string, (ScreenInfo Screen, IReadOnlyDictionary<string, object?>? Row, IDisposable Window)>
        _windows = [];

    private bool _syncPosted;
    private bool _disposed;

    public ScreenWindows(string name, Func<ScreenInfo, IReadOnlyDictionary<string, object?>?, IDisposable> make)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(make);
        _name = name;
        _make = make;
        _shell = Shell.Current;
    }

    void IShellPart.Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_rows is not null)
            return;
        _rows = new ScreenRows(_name);
        _rows.PropertyChanged += (_, _) => PostSync();
        _shell.Screens.CollectionChanged += OnScreensChanged;
        Sync();
    }

    private void OnScreensChanged(object? sender, EventArgs e)
    {
        Sync();
    }

    private void PostSync()
    {
        if (_syncPosted || _disposed)
            return;
        _syncPosted = true;
        _shell.Loop.Post(() =>
        {
            _syncPosted = false;
            Sync();
        });
    }

    private void Sync()
    {
        if (_disposed)
            return;
        var screens = _shell.Screens.ToArray();
        foreach (var id in _windows.Keys.Where(id => !Array.Exists(screens, screen => screen.Id == id)).ToArray())
            Try($"dispose the window of {id}", () => Drop(id));
        foreach (var screen in screens)
        {
            var row = RowOf(screen);
            if (_windows.TryGetValue(screen.Id, out var kept))
            {
                if (Same(kept.Screen, screen) && TomlDocument.Same(kept.Row, row))
                    continue;
                Try($"dispose the window of {screen.Id}", () => Drop(screen.Id));
            }

            Try($"make the window of {screen.Id}", () => Make(screen, row));
            if (_disposed)
                return;
        }
    }

    private void Make(ScreenInfo screen, IReadOnlyDictionary<string, object?>? row)
    {
        var identity = $"{screen.Manufacturer} / {screen.Model} / {screen.SerialNumber}";
        var table = $"[[{TomlDocument.Key(_name)}.screen]]";
        _shell.Log.Info(_name, row is null ? $"{identity} has no {table} row" : $"{identity} uses its {table} row");
        var window = _make(screen, row);
        try
        {
            (window as IShellPart)?.Start();
        }
        catch
        {
            window.Dispose();
            throw;
        }

        if (_disposed)
            window.Dispose();
        else
            _windows[screen.Id] = (screen, row, window);
    }

    private void Drop(string id)
    {
        if (!_windows.Remove(id, out var dropped))
            return;
        dropped.Window.Dispose();
    }

    private void Try(string what, Action part)
    {
        try
        {
            part();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _shell.Log.Error(_name, $"Could not {what}: {e}");
        }
    }

    private static bool Same(ScreenInfo kept, ScreenInfo now)
    {
        return kept.Manufacturer == now.Manufacturer && kept.Model == now.Model &&
               kept.SerialNumber == now.SerialNumber;
    }

    private IReadOnlyDictionary<string, object?>? RowOf(ScreenInfo screen)
    {
        return _rows!.Screen?.FirstOrDefault(row =>
            row.GetValueOrDefault("manufacturer") as string == screen.Manufacturer &&
            row.GetValueOrDefault("model") as string == screen.Model &&
            row.GetValueOrDefault("serial") as string == screen.SerialNumber);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shell.Remove(this);
        _shell.Screens.CollectionChanged -= OnScreensChanged;
        _rows?.Dispose();
        var windows = _windows.Values.Select(entry => entry.Window).ToArray();
        _windows.Clear();
        Shell.DisposeEach(windows);
    }

    private sealed partial class ScreenRows(string name) : Preferences(name)
    {
        [Preference]
        public partial IReadOnlyList<IReadOnlyDictionary<string, object?>>? Screen { get; set; }
    }
}
