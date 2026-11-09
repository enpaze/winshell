using System.Text;

namespace Winshell;

/// <summary>
/// The shell's preferences file and the <see cref="Preferences"/> sections that read it. Reads, writes and every
/// section's changes run on the shell loop.
/// </summary>
internal sealed class PreferencesFile : IDisposable
{
    private static readonly IReadOnlyDictionary<string, object?> Nothing = new Dictionary<string, object?>();

    private readonly string _path;
    private readonly IEventLoop _loop;
    private readonly Log _log;
    private readonly FileWatch _watch;
    private readonly List<Preferences> _sections = [];

    /// <summary>The file as last read or written, or null while it is missing.</summary>
    private TomlDocument? _document;

    /// <summary>The text last read or written, so the watch can tell a hand save from this file's own write.</summary>
    private string? _text;

    /// <summary>Why the file could not be used, as <c>path[line:column]: message</c>, or empty.</summary>
    private string _error = "";

    private bool _writePending;
    private bool _checkPending;

    /// <summary>The turns of the loop the check still waits, counted again from each section made.</summary>
    private int _checkTurns;
    private bool _disposed;

    /// <param name="path">The file. It and its folder need not exist yet.</param>
    /// <param name="loop">Where a change to the file arrives and a write runs.</param>
    /// <param name="log">Takes the warnings.</param>
    public PreferencesFile(string path, IEventLoop loop, Log log)
    {
        _path = Path.GetFullPath(path);
        _loop = loop;
        _log = log;
        // Watching first, so a save between the two is not missed.
        _watch = new FileWatch(_path, loop, Follow);
        Follow();
    }

    public void Add(Preferences section)
    {
        // A save the watch has not delivered yet is read now, so the section starts from the file as it is.
        Follow();
        _sections.Add(section);
        Load(section, null);
        PostCheck();
    }

    public void Remove(Preferences section)
    {
        if (_disposed)
            return;
        if (section.Assigned.Count > 0)
            Write();
        _sections.Remove(section);
    }

    /// <summary>Writes the assigned values of every section on the next turn of the loop.</summary>
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

    /// <summary>
    /// Writes a missing file and names sections no component reads, once a whole turn of the loop has passed with no
    /// section made: a component such as a bar makes its section on the turn after it is made, once its properties are
    /// set.
    /// </summary>
    private void PostCheck()
    {
        _checkTurns = 2;
        if (_checkPending || _disposed)
            return;
        _checkPending = true;
        _loop.Post(Check);
    }

    private void Check()
    {
        if (_disposed)
            return;
        if (--_checkTurns > 0)
        {
            _loop.Post(Check);
            return;
        }

        _checkPending = false;
        if (_document is null && _sections.Count > 0 && TomlDocument.Load(_path).Kind == TomlRead.Missing)
            WriteDefaults();
        WarnUnknown();
    }

    /// <summary>Takes in the file as it is now. Each section changes only the properties whose keys changed.</summary>
    private void Follow()
    {
        if (_writePending)
            return;
        var load = TomlDocument.Load(_path);
        switch (load.Kind)
        {
            case TomlRead.Unreadable:
                // An editor can hold the file while it saves. That says nothing about the file.
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

        PostCheck();
    }

    /// <summary>
    /// The sections, each while it is still attached: loading one runs its handlers, which can make or dispose others.
    /// </summary>
    private IEnumerable<Preferences> Attached()
    {
        return _sections.ToArray().Where(_sections.Contains);
    }

    /// <summary>Keeps every section as it is and says why the file could not be used.</summary>
    private void Failed(string error, string warning)
    {
        Report(error);
        Warn(warning);
    }

    /// <summary>Tells <c>winshell dev</c> when the file stops or starts being usable.</summary>
    private void Report(string error)
    {
        if (error == _error)
            return;
        _error = error;
        _log.ReportFileError(_path, error);
    }

    /// <summary>Loads <paramref name="section"/> from the file, leaving <paramref name="keep"/> as they are.</summary>
    private void Load(Preferences section, IReadOnlySet<TomlKey>? keep)
    {
        if (Values(section, _document, out var where) is { } values)
            section.Keys.Apply(values, where, keep, section.Defaults);
    }

    /// <summary>
    /// The part of <paramref name="document"/> that is <paramref name="section"/>'s, and how to find a key of it. Null
    /// when the section is not a table, which is reported.
    /// </summary>
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
            // A table is a component's section, unless this reads a key by that name.
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

    /// <summary>Puts the assigned values of every section into the file, which a hand save may have changed.</summary>
    private void Write()
    {
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
    }

    /// <summary>Puts the text down, or returns false with the assigned values still waiting.</summary>
    private bool Put(string next, string? read, bool bom)
    {
        try
        {
            var written = AtomicFile.Save(_path, next, bom, read) == AtomicSave.Saved;
            // A hand save landed during this write. The next pass reads it and writes the assigned values into it.
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

    private void WriteDefaults()
    {
        try
        {
            AtomicFile.Save(_path, Defaults(), false, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Warn($"failed to write {_path}: {e.Message}");
        }
    }

    /// <summary>
    /// The file a missing one is written as: the configuration's own keys, then a header for each section, each key
    /// commented out at its default under its comment.
    /// </summary>
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
            foreach (var key in group.SelectMany(section => section.Keys.All.Select(key => (Section: section, Key: key)))
                         .DistinctBy(pair => pair.Key.Name))
            {
                if (TomlDocument.Format(TomlDocument.Plain(key.Section.Commented(key.Key))) is not { } value)
                    continue;
                text.Append('\n');
                foreach (var line in TomlDocument.CommentLines(key.Key.Comment))
                    text.Append(line).Append('\n');
                text.Append("# ").Append(TomlDocument.Key(key.Key.Name)).Append(" = ").Append(value).Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>Names each section of the file that no component reads. It stays in the file.</summary>
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

    /// <summary>Stops following the file. A write that was on its way runs now.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _watch.Dispose();
        if (_writePending)
            Write();
        _disposed = true;
        _sections.Clear();
    }
}
