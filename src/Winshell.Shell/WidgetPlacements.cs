using System.Text;

namespace Winshell;

/// <summary>The shell's user-owned lists. All reads, area changes and rebuilds run on the shell loop.</summary>
internal sealed class WidgetPlacements : IDisposable
{
    private readonly Shell _shell;
    private readonly string _path;
    private readonly FileWatch _watch;
    private readonly List<WidgetArea> _areas = [];
    private Dictionary<string, IReadOnlyList<Placement>> _lists = new(StringComparer.Ordinal);
    private TomlDocument? _document;
    private string? _text;
    private bool _pending;
    private bool _disposed;

    public WidgetPlacements(Shell shell)
    {
        _shell = shell;
        _path = Path.Combine(shell.ConfigRoot, "widgets", shell.ShellId + ".toml");
        _watch = new FileWatch(_path, shell.Loop, Follow);
        Follow();
    }

    public void Add(WidgetArea area)
    {
        _areas.Add(area);
        Changed(area);
    }

    public void Remove(WidgetArea area)
    {
        _areas.Remove(area);
    }

    public void Changed(WidgetArea area)
    {
        Apply(area);
        Schedule();
    }

    private void Schedule()
    {
        if (_pending || _disposed)
            return;
        _pending = true;
        // Initializers and the rest of configuration.Start finish before defaults are written or names diagnosed.
        _shell.Loop.Post(() =>
        {
            _pending = false;
            if (_disposed)
                return;
            WriteDefaults();
            WarnUnknown();
        });
    }

    private void Follow()
    {
        var load = TomlDocument.Load(_path);
        if (load.Kind == TomlRead.Missing)
        {
            _text = null;
            _document = null;
            _lists.Clear();
        }
        else
        {
            if (load.Text is not null && load.Text == _text)
                return;
            if (load.Kind != TomlRead.Parsed)
            {
                _shell.Log.Warn("widgets", load.Describe(_path));
                return;
            }

            var document = load.Document!;
            var lists = new Dictionary<string, IReadOnlyList<Placement>>(StringComparer.Ordinal);
            foreach (var (name, value) in document.Values)
            {
                if (value is not IReadOnlyList<object?> rows || rows.Any(row =>
                        row is not IReadOnlyDictionary<string, object?> table ||
                        table.GetValueOrDefault("widget") is not string widget || string.IsNullOrWhiteSpace(widget)))
                {
                    _shell.Log.Warn("widgets",
                        $"{document.LocationOf(name)}: {name} must be a list of tables with a widget name; keeping the last valid lists.");
                    return;
                }

                lists[name] = rows.Cast<IReadOnlyDictionary<string, object?>>()
                    .Select(table => new Placement((string)table["widget"]!, table
                        .Where(pair => pair.Key != "widget").ToDictionary(pair => pair.Key, pair => pair.Value!)))
                    .ToArray();
            }

            _text = load.Text;
            _document = document;
            _lists = lists;
        }

        foreach (var area in _areas.ToArray())
            Apply(area);
        Schedule();
    }

    private void Apply(WidgetArea area)
    {
        area.ApplyPlacements(area.Name.Length > 0 ? _lists.GetValueOrDefault(area.Name) : null);
    }

    private void WarnUnknown()
    {
        foreach (var name in _lists.Keys)
            if (!_areas.Exists(area => area.Name == name))
                _shell.Log.Warn("widgets", $"{_document!.LocationOf(name)}: no widget area named '{name}'; the entry was kept in the file.");
    }

    private void WriteDefaults()
    {
        if (TomlDocument.Load(_path).Kind != TomlRead.Missing)
            return;
        var text = new StringBuilder("# Widget placements. Uncomment an area's whole list to replace its defaults.\n# Remove its entry to use the shell's defaults again. Use name = [] for an empty area.\n");
        // Top-level empty lists must precede table headers, or TOML would put them in the last placement.
        foreach (var area in _areas.Where(area => area.Name.Length > 0).DistinctBy(area => area.Name)
                     .OrderBy(area => area.Defaults.Count > 0))
        {
            text.Append('\n');
            if (area.Defaults.Count == 0)
                text.Append("# ").Append(TomlDocument.Format(area.Name)).Append(" = []\n");
            foreach (var placement in area.Defaults)
            {
                text.Append("# [[").Append(TomlDocument.Format(area.Name)).Append("]]\n# widget = ")
                    .Append(TomlDocument.Format(placement.Widget)).Append('\n');
                if (placement.Settings is not { } settings)
                    continue;
                foreach (var (key, value) in settings.Where(pair => pair.Key != "widget"))
                    text.Append("# ").Append(TomlDocument.Format(key)).Append(" = ")
                        .Append(TomlDocument.Format(TomlDocument.Plain(value))).Append('\n');
            }
        }

        try
        {
            AtomicFile.Save(_path, text.ToString(), false, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _shell.Log.Warn("widgets", $"Could not write {_path}: {e.Message}");
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _watch.Dispose();
        _areas.Clear();
    }
}
