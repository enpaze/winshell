using System.Text;

namespace Winshell;

internal sealed class PreferencesFile : IDisposable
{
    private static readonly IReadOnlyDictionary<string, object?> Nothing = new Dictionary<string, object?>();

    private readonly string _path;
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly FileWatch _watch;
    private readonly List<Preferences> _sections = [];

    private readonly List<Preferences> _made = [];

    private TomlDocument? _document;

    private string? _text;

    private string _error = "";

    private bool _writePending;
    private bool _commitPending;

    private bool _afterStart;

    private bool _startupPosted;
    private bool _disposed;

    public PreferencesFile(string path, IEventLoop loop, Log log)
    {
        _path = Path.GetFullPath(path);
        _loop = loop;
        _log = log;
        _watch = new FileWatch(_path, loop, Follow);
        Follow();
    }

    internal void StartReturned()
    {
        if (_disposed || _startupPosted)
            return;
        _startupPosted = true;
        _loop.Post(() =>
        {
            if (_disposed)
                return;
            _afterStart = true;
            Commit();
        });
    }

    public void Add(Preferences section)
    {
        Follow();
        _sections.Add(section);
        _made.Add(section);
        Load(section, null);
        PostCommit();
    }

    public void Remove(Preferences section)
    {
        if (_disposed)
            return;
        if (section.Assigned.Count > 0)
            Write(waitForStart: false);
        _sections.Remove(section);
        _made.Remove(section);
    }

    public void PostWrite()
    {
        if (_writePending || _disposed)
            return;
        _writePending = true;
        _loop.Post(() =>
        {
            if (_writePending && !_disposed)
                Write();
        });
    }

    public void Warn(string message)
    {
        _log.Warn("preferences", message);
    }

    private void PostCommit()
    {
        if (!_afterStart || _commitPending || _disposed)
            return;
        _commitPending = true;
        _loop.Post(Commit);
    }

    private void Commit()
    {
        if (_disposed)
            return;
        _commitPending = false;
        var load = TomlDocument.Load(_path);
        if (_writePending ||
            (_sections.Count > 0 &&
             (load.Kind == TomlRead.Missing || (load.Document is { } document && Unwritten(document).Any()))))
            Write();
        else if (load.Kind == TomlRead.Parsed)
            _made.Clear();
        WarnUnknown();
    }

    private void Follow()
    {
        if (_writePending)
            return;
        var load = TomlDocument.Load(_path);
        switch (load.Kind)
        {
            case TomlRead.Unreadable:
                _log.Debug("io", $"Preferences: failed to read {_path}: {load.Message}");
                return;
            case TomlRead.TooLarge:
                _text = null;
                Failed(load.Describe(_path), $"failed to read {load.Describe(_path)}");
                return;
            case TomlRead.Invalid:
                if (load.Text == _text)
                    return;
                _text = load.Text;
                Failed(load.Describe(_path), $"failed to parse {load.Describe(_path)}");
                return;
            case TomlRead.Missing:
                if (_text is null && _document is null)
                    return;
                _text = null;
                _document = null;
                break;
            case TomlRead.Parsed:
                if (load.Text == _text)
                    return;
                _text = load.Text;
                _document = load.Document;
                break;
        }

        Report("");
        foreach (var section in Attached())
        {
            Load(section, null);
            section.Assigned.Clear();
        }

        PostCommit();
    }

    private IEnumerable<Preferences> Attached()
    {
        return _sections.ToArray().Where(_sections.Contains);
    }

    private void Failed(string error, string warning)
    {
        Report(error);
        Warn(warning);
    }

    private void Report(string error)
    {
        if (error == _error)
            return;
        _error = error;
        _log.ReportFileError(_path, error);
    }

    private void Load(Preferences section, IReadOnlySet<TomlKey>? keep)
    {
        if (Values(section, _document, out var where) is { } values)
            section.Keys.Apply(values, where, keep, section.Defaults);
    }

    private IReadOnlyDictionary<string, object?>? Values(Preferences section, TomlDocument? document,
        out Func<string, string> where)
    {
        var name = section.Section;
        where = key => document is null ? _path
            : name.Length == 0 ? document.LocationOf(key)
            : document.LocationOf(name, key);
        if (document is null)
            return Nothing;
        if (name.Length == 0)
            return document.Values.Where(pair => pair.Value is not IReadOnlyDictionary<string, object?> ||
                                                 section.Keys.All.Any(key => key.Name == pair.Key))
                .ToDictionary();
        switch (document.Values.GetValueOrDefault(name))
        {
            case null:
                return Nothing;
            case IReadOnlyDictionary<string, object?> table:
                return table;
            case var value:
                Warn($"{document.LocationOf(name)}: {name} should be a section, [{name}], not {TomlDocument.Format(value)}, so it was left unchanged");
                return null;
        }
    }

    private void Write(bool waitForStart = true)
    {
        if (waitForStart && !_afterStart)
            return;
        _writePending = false;
        var load = TomlDocument.Load(_path);
        var document = load.Document;
        if (load.Kind == TomlRead.Missing)
            document = TomlDocument.TryParse(_path, Defaults(), out var defaults, out _)
                ? defaults
                : TomlDocument.Empty(_path);
        if (document is null)
        {
            Failed(load.Describe(_path), $"failed to parse {load.Describe(_path)}; the file was left unchanged");
            return;
        }

        if (load.Text != _text)
        {
            _document = document;
            foreach (var section in Attached())
                Load(section, section.Assigned);
        }

        foreach (var group in Unwritten(document))
            document.AddTable(group.Key, Commented(group));

        foreach (var section in Attached().Where(section => section.Assigned.Count > 0))
            if (section.Section.Length == 0)
                foreach (var key in section.Assigned)
                    document.Set(key.Name, key.Value(section), key.Comment);
            else
                document.Set(section.Section,
                    section.Assigned.ToDictionary(key => key.Name, key => TomlDocument.Plain(key.Value(section))));

        var next = document.ToString();
        if (next != load.Text && !Put(next, load.Text, load.Bom))
            return;
        _text = next;
        _document = document;
        Report("");
        foreach (var section in _sections)
            section.Assigned.Clear();
        _made.Clear();
    }

    private IEnumerable<IGrouping<string, Preferences>> Unwritten(TomlDocument document)
    {
        var due = _sections
            .Where(section => section.Section.Length > 0 && (section.Assigned.Count > 0 || _made.Contains(section)) &&
                              !document.Values.ContainsKey(section.Section))
            .Select(section => section.Section)
            .ToHashSet();
        return _sections.Where(section => due.Contains(section.Section)).GroupBy(section => section.Section);
    }

    private bool Put(string next, string? read, bool bom)
    {
        try
        {
            var written = AtomicFile.Save(_path, next, bom, read) == AtomicSave.Saved;
            if (!written)
                PostWrite();
            return written;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Warn($"failed to write {_path}: {e.Message}");
            return false;
        }
    }

    private string Defaults()
    {
        var text = new StringBuilder("""
            # This shell's preferences. A key that is commented out or missing keeps its default: remove its # to change it.
            # The keys before the first [section] are the configuration's own, and each section is one component's.

            """);
        foreach (var group in _sections.GroupBy(section => section.Section).OrderBy(group => group.Key.Length > 0))
        {
            if (group.Key.Length > 0)
                text.Append('\n').Append('[').Append(TomlDocument.Key(group.Key)).Append("]\n");
            foreach (var line in Commented(group))
                text.Append(line).Append('\n');
        }

        return text.ToString();
    }

    private static IEnumerable<string> Commented(IEnumerable<Preferences> group)
    {
        foreach (var (section, key) in group
                     .SelectMany(section => section.Keys.All.Select(key => (Section: section, Key: key)))
                     .DistinctBy(pair => pair.Key.Name))
        {
            if (TomlDocument.Format(TomlDocument.Plain(section.Commented(key))) is not { } value)
                continue;
            yield return "";
            foreach (var line in TomlDocument.CommentLines(key.Comment))
                yield return line;
            yield return "# " + TomlDocument.Key(key.Name) + " = " + value;
        }
    }

    private void WarnUnknown()
    {
        if (_document is not { } document)
            return;
        foreach (var (name, value) in document.Values)
            if (value is IReadOnlyDictionary<string, object?> &&
                !_sections.Exists(section => section.Section == name ||
                                             (section.Section.Length == 0 && section.Keys.All.Any(key => key.Name == name))))
                Warn($"{document.LocationOf(name)}: no component named '{name}'; the section was kept in the file.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _watch.Dispose();
        if (_writePending)
            Write(waitForStart: false);
        _disposed = true;
        _sections.Clear();
        _made.Clear();
    }
}
