namespace Winshell;

/// <summary>
/// One window on each screen for a component, such as a bar or a dock. It makes a screen's window when the screen
/// appears and disposes it when the screen goes, and leaves every other screen's window as it is, so their widgets keep
/// their state. A screen whose size, scale or work area changes keeps its window, and the screen it was made with: read
/// <see cref="Shell.Screens"/> for the screen as it is now.
/// <code>
/// var dock = new ScreenWindows("dock", (screen, row) => new PanelWindow
/// {
///     Screen = screen,
///     Anchors = row?.GetValueOrDefault("edge") is "top"
///         ? Edges.Left | Edges.Right | Edges.Top
///         : Edges.Left | Edges.Right | Edges.Bottom,
///     Height = 48,
///     ReservedSize = 48,
///     Visible = true
/// });
/// </code>
/// A screen that differs from the rest has a row in the component's section of the preferences file, a
/// <c>[[dock.screen]]</c> table with its <c>manufacturer</c>, <c>model</c> and <c>serial</c> as strings. A row matches
/// a screen when all three equal the screen's <see cref="ScreenInfo.Manufacturer"/>, <see cref="ScreenInfo.Model"/>
/// and <see cref="ScreenInfo.SerialNumber"/>, so an empty one is written <c>""</c>, and the first match wins. The name
/// Windows numbers, such as <c>DISPLAY1</c>, is never matched: Windows renumbers it. A row for a screen that is not
/// connected stays in the file and matches no other screen. When a screen's row changes, its window is disposed and
/// made again from the new row, on the next turn of the loop, once the whole save is read. The log says, under the
/// component's name, which screens have a row of their own.
/// <para>
/// It is a shortcut, not a limit: a configuration can still make any window on any screen itself. Make one on the
/// shell's thread and dispose it when the component goes, which disposes its windows.
/// </para>
/// </summary>
public sealed class ScreenWindows : IDisposable
{
    private readonly string _name;
    private readonly Func<ScreenInfo, IReadOnlyDictionary<string, object?>?, IDisposable> _make;
    private readonly Shell _shell;
    private readonly ScreenRows _rows;

    /// <summary>Each screen's window, by the screen's id, with the screen and the row it was made for.</summary>
    private readonly Dictionary<string, (ScreenInfo Screen, IReadOnlyDictionary<string, object?>? Row, IDisposable Window)>
        _windows = [];

    private bool _syncPosted;
    private bool _disposed;

    /// <summary>Makes the window of every screen there is now, and follows the screens and the rows from then on.</summary>
    /// <param name="name">The component's name, which names its section of the preferences file, as in <c>[bar]</c>.</param>
    /// <param name="make">
    /// Makes the window for a screen, given the screen and its row, or null when it has none. The row is the table as
    /// the file has it, the three matching keys included, with each value as TOML reads it: a string, a bool, a long,
    /// a double, a list or a table. Return the window, or anything disposable that holds it with what else the
    /// screen's window needs, such as its widget areas. A function that throws is logged under the component's name,
    /// and that screen has no window until the screens or the rows change.
    /// </param>
    public ScreenWindows(string name, Func<ScreenInfo, IReadOnlyDictionary<string, object?>?, IDisposable> make)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(make);
        _name = name;
        _make = make;
        _shell = Shell.Current;
        _rows = new ScreenRows(name);
        // A save can change a row and the component's other keys in one read. Following once, on the next turn, sees
        // all of it, so a window is made from the component's new keys too.
        _rows.PropertyChanged += (_, _) => PostSync();
        _shell.Screens.CollectionChanged += OnScreensChanged;
        Sync();
    }

    // Each change is followed as it is made. The platform adds a renumbered screen's new id before it removes the old
    // one, so that monitor has two windows until the removal, in the same turn.
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

    /// <summary>
    /// Disposes the windows of screens that went, or that are another monitor now or have another row, and makes the
    /// windows that are missing.
    /// </summary>
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
            // The window function or a window's disposal can dispose this.
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

    /// <summary>Runs one screen's part, so a window function that throws leaves the other screens' windows.</summary>
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

    /// <summary>Whether two records of one screen id are the same monitor, which a size or scale change leaves.</summary>
    private static bool Same(ScreenInfo kept, ScreenInfo now)
    {
        return kept.Manufacturer == now.Manufacturer && kept.Model == now.Model &&
               kept.SerialNumber == now.SerialNumber;
    }

    /// <summary>The first row whose manufacturer, model and serial all equal the screen's, or null when none does.</summary>
    private IReadOnlyDictionary<string, object?>? RowOf(ScreenInfo screen)
    {
        return _rows.Screen?.FirstOrDefault(row =>
            row.GetValueOrDefault("manufacturer") as string == screen.Manufacturer &&
            row.GetValueOrDefault("model") as string == screen.Model &&
            row.GetValueOrDefault("serial") as string == screen.SerialNumber);
    }

    /// <summary>
    /// Disposes every window and stops following the screens and the rows. All are attempted even if one disposal
    /// throws.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shell.Screens.CollectionChanged -= OnScreensChanged;
        _rows.Dispose();
        var windows = _windows.Values.Select(entry => entry.Window).ToArray();
        _windows.Clear();
        Shell.DisposeEach(windows);
    }

    /// <summary>The <c>screen</c> rows of the component's section, each one screen that differs.</summary>
    private sealed class ScreenRows(string name) : Preferences(name)
    {
        public IReadOnlyList<IReadOnlyDictionary<string, object?>>? Screen
        {
            get;
            set => Set(ref field, value);
        }
    }
}
